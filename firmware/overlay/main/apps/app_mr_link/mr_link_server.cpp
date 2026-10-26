/*
 * SPDX-License-Identifier: MIT
 */
#include "mr_link_server.h"
#include <algorithm>
#include <cstdio>
#include <cstring>
#include <esp_mac.h>
#include <esp_random.h>
#include <mdns.h>
#include <mooncake_log.h>
#include <sys/socket.h>
#include <unistd.h>

namespace mr_link {

static const std::string _tag = "MOGUMOGU/ws";

static constexpr const char* kWsPath = "/ws";
static constexpr size_t kMaxFrameSize = 1024;  // messages are tiny; refuse anything bigger

Server& GetServer()
{
    static Server instance;
    return instance;
}

Server::~Server()
{
    stop();
}

std::string Server::generateToken()
{
    char buf[17];
    snprintf(buf, sizeof(buf), "%08x%08x", static_cast<unsigned>(esp_random()), static_cast<unsigned>(esp_random()));
    return buf;
}

bool Server::start(const Config& config)
{
    std::lock_guard<std::mutex> lock(_mutex);
    if (_server != nullptr) {
        return true;
    }
    if (config.token.empty()) {
        mclog::tagError(_tag, "start refused: token is empty");
        return false;
    }
    _config = config;

    httpd_config_t cfg = HTTPD_DEFAULT_CONFIG();
    cfg.server_port = _config.port;
    cfg.ctrl_port = 32769;  // avoid clashing with another httpd instance on the default control port
    cfg.max_open_sockets = 4;
    cfg.lru_purge_enable = true;  // never get stuck on dead sockets
    cfg.stack_size = 6144;
    cfg.close_fn = onSessionClose;
    // Detect a vanished Quest (Wi-Fi off, app killed) in about 20 s
    cfg.keep_alive_enable = true;
    cfg.keep_alive_idle = 5;
    cfg.keep_alive_interval = 5;
    cfg.keep_alive_count = 3;

    if (httpd_start(&_server, &cfg) != ESP_OK) {
        _server = nullptr;
        mclog::tagError(_tag, "httpd_start failed");
        return false;
    }

    httpd_uri_t uri = {};
    uri.uri = kWsPath;
    uri.method = HTTP_GET;
    uri.handler = wsHandler;
    uri.user_ctx = this;
    uri.is_websocket = true;
    uri.handle_ws_control_frames = false;  // httpd answers PING and CLOSE by itself
    // httpd never calls uri.handler for the handshake request itself, so the token check and the client
    // registration have to run from this callback (needs CONFIG_HTTPD_WS_POST_HANDSHAKE_CB_SUPPORT=y).
    uri.ws_post_handshake_cb = onHandshakeDone;
    if (httpd_register_uri_handler(_server, &uri) != ESP_OK) {
        mclog::tagError(_tag, "register ws handler failed");
        httpd_stop(_server);
        _server = nullptr;
        return false;
    }

    startMdns();
    mclog::tagInfo(_tag, "listening on port {} ({}.local)", _config.port, _config.hostname);
    return true;
}

void Server::stop()
{
    httpd_handle_t hd = nullptr;
    bool had_client = false;
    {
        std::lock_guard<std::mutex> lock(_mutex);
        hd = _server;
        _server = nullptr;
        had_client = _client_fd >= 0;
        _client_fd = -1;
    }
    if (hd == nullptr) {
        return;
    }
    stopMdns();
    // Must not hold _mutex: httpd_stop() runs onSessionClose() for each open session
    httpd_stop(hd);
    if (had_client) {
        notifyConnection(false);
    }
    mclog::tagInfo(_tag, "stopped");
}

bool Server::isRunning() const
{
    std::lock_guard<std::mutex> lock(_mutex);
    return _server != nullptr;
}

bool Server::hasClient() const
{
    std::lock_guard<std::mutex> lock(_mutex);
    return _client_fd >= 0;
}

int Server::addMessageHandler(const std::string& type, MessageHandler handler)
{
    std::lock_guard<std::mutex> lock(_mutex);
    int id = _next_id++;
    _message_handlers.push_back({id, type, std::move(handler)});
    return id;
}

int Server::addConnectionHandler(ConnectionHandler handler)
{
    std::lock_guard<std::mutex> lock(_mutex);
    int id = _next_id++;
    _connection_handlers.push_back({id, std::move(handler)});
    return id;
}

void Server::removeHandler(int id)
{
    std::lock_guard<std::mutex> lock(_mutex);
    _message_handlers.erase(std::remove_if(_message_handlers.begin(), _message_handlers.end(),
                                           [id](const MessageEntry& e) { return e.id == id; }),
                            _message_handlers.end());
    _connection_handlers.erase(std::remove_if(_connection_handlers.begin(), _connection_handlers.end(),
                                              [id](const ConnectionEntry& e) { return e.id == id; }),
                               _connection_handlers.end());
}

bool Server::sendText(const std::string& json)
{
    httpd_handle_t hd = nullptr;
    int fd = -1;
    {
        std::lock_guard<std::mutex> lock(_mutex);
        hd = _server;
        fd = _client_fd;
    }
    if (hd == nullptr || fd < 0) {
        return false;
    }
    httpd_ws_frame_t pkt = {};
    pkt.type = HTTPD_WS_TYPE_TEXT;
    pkt.final = true;
    pkt.payload = reinterpret_cast<uint8_t*>(const_cast<char*>(json.data()));
    pkt.len = json.size();
    return httpd_ws_send_frame_async(hd, fd, &pkt) == ESP_OK;
}

esp_err_t Server::wsHandler(httpd_req_t* req)
{
    return static_cast<Server*>(req->user_ctx)->handleRequest(req);
}

void Server::onSessionClose(httpd_handle_t hd, int sockfd)
{
    // A custom close_fn replaces the default one, so the socket must be closed here
    close(sockfd);

    Server& self = GetServer();
    bool was_client = false;
    {
        std::lock_guard<std::mutex> lock(self._mutex);
        if (self._client_fd == sockfd) {
            self._client_fd = -1;
            was_client = true;
        }
    }
    if (was_client) {
        mclog::tagInfo(_tag, "client disconnected");
        self.notifyConnection(false);
    }
}

bool Server::checkToken(httpd_req_t* req) const
{
    char query[128];
    if (httpd_req_get_url_query_str(req, query, sizeof(query)) != ESP_OK) {
        return false;
    }
    char given[64];
    if (httpd_query_key_value(query, "token", given, sizeof(given)) != ESP_OK) {
        return false;
    }
    std::string expected;
    {
        std::lock_guard<std::mutex> lock(_mutex);
        expected = _config.token;
    }
    // Constant-time compare
    size_t given_len = strlen(given);
    unsigned diff = static_cast<unsigned>(given_len ^ expected.size());
    for (size_t i = 0; i < given_len; i++) {
        diff |= static_cast<unsigned char>(given[i]) ^ static_cast<unsigned char>(expected[i % expected.size()]);
    }
    return diff == 0;
}

esp_err_t Server::onHandshakeDone(httpd_req_t* req)
{
    return static_cast<Server*>(req->user_ctx)->handleHandshake(req);
}

esp_err_t Server::handleHandshake(httpd_req_t* req)
{
    const int fd = httpd_req_to_sockfd(req);

    // The handshake response is already sent, so a bad token can only be rejected by closing the
    // connection (returning an error does that).
    if (!checkToken(req)) {
        mclog::tagWarn(_tag, "rejected connection (bad token)");
        return ESP_FAIL;
    }
    int old_fd = -1;
    {
        std::lock_guard<std::mutex> lock(_mutex);
        old_fd = _client_fd;
        _client_fd = fd;
    }
    if (old_fd >= 0 && old_fd != fd) {
        // Only one client: the newest authenticated one wins (the old one may be a half-open socket)
        mclog::tagInfo(_tag, "replacing previous client");
        httpd_sess_trigger_close(req->handle, old_fd);
    }
    mclog::tagInfo(_tag, "client connected");
    sendHello(fd);
    notifyConnection(true);
    return ESP_OK;
}

esp_err_t Server::handleRequest(httpd_req_t* req)
{
    const int fd = httpd_req_to_sockfd(req);

    // WebSocket data frame (the handshake never reaches here, see onHandshakeDone).
    // Only the authenticated client may talk.
    {
        std::lock_guard<std::mutex> lock(_mutex);
        if (fd != _client_fd) {
            return ESP_FAIL;
        }
    }

    httpd_ws_frame_t pkt = {};
    pkt.type = HTTPD_WS_TYPE_TEXT;
    esp_err_t ret = httpd_ws_recv_frame(req, &pkt, 0);  // get the length only
    if (ret != ESP_OK) {
        return ret;
    }
    if (pkt.len > kMaxFrameSize) {
        mclog::tagWarn(_tag, "frame too large ({} bytes), closing", pkt.len);
        return ESP_FAIL;
    }
    std::vector<uint8_t> buf(pkt.len + 1, 0);
    pkt.payload = buf.data();
    ret = httpd_ws_recv_frame(req, &pkt, pkt.len);
    if (ret != ESP_OK) {
        return ret;
    }
    if (pkt.type != HTTPD_WS_TYPE_TEXT || !pkt.final) {
        mclog::tagInfo(_tag, "ignored non-text or fragmented frame");
        return ESP_OK;
    }
    dispatch(reinterpret_cast<const char*>(buf.data()), pkt.len);
    return ESP_OK;
}

void Server::dispatch(const char* payload, size_t len)
{
    ArduinoJson::JsonDocument doc;
    ArduinoJson::DeserializationError error = ArduinoJson::deserializeJson(doc, payload, len);
    if (error) {
        mclog::tagWarn(_tag, "invalid json: {}", error.c_str());
        return;
    }
    const char* type = doc["type"].is<const char*>() ? doc["type"].as<const char*>() : nullptr;
    if (type == nullptr) {
        mclog::tagWarn(_tag, "message without type ignored");
        return;
    }

    // Copy the matching handlers so that they can be called without holding the lock
    std::vector<MessageHandler> targets;
    {
        std::lock_guard<std::mutex> lock(_mutex);
        for (const auto& e : _message_handlers) {
            if (e.type == type) {
                targets.push_back(e.handler);
            }
        }
    }
    if (targets.empty()) {
        mclog::tagInfo(_tag, "unknown type ignored: {}", type);
        return;
    }
    for (const auto& h : targets) {
        h(doc);
    }
}

void Server::notifyConnection(bool connected)
{
    std::vector<ConnectionHandler> targets;
    {
        std::lock_guard<std::mutex> lock(_mutex);
        for (const auto& e : _connection_handlers) {
            targets.push_back(e.handler);
        }
    }
    for (const auto& h : targets) {
        h(connected);
    }
}

void Server::sendHello(int fd)
{
    uint8_t mac[6] = {};
    esp_read_mac(mac, ESP_MAC_WIFI_STA);
    char mac_str[18];
    snprintf(mac_str, sizeof(mac_str), "%02X:%02X:%02X:%02X:%02X:%02X", mac[0], mac[1], mac[2], mac[3], mac[4], mac[5]);

    ArduinoJson::JsonDocument doc;
    doc["type"] = "hello";
    doc["proto"] = kProtocolVersion;
    doc["mac"] = mac_str;
#ifdef FIRMWARE_VERSION
    doc["fw"] = "V" FIRMWARE_VERSION;
#else
    doc["fw"] = "unknown";
#endif
    std::string out;
    ArduinoJson::serializeJson(doc, out);
    sendText(out);
}

void Server::startMdns()
{
    // mdns_init() is safe to call when another module has already initialized it
    esp_err_t err = mdns_init();
    if (err != ESP_OK && err != ESP_ERR_INVALID_STATE) {
        mclog::tagError(_tag, "mdns_init failed: {}", static_cast<int>(err));
        return;
    }
    mdns_hostname_set(_config.hostname.c_str());
    mdns_instance_name_set("StackChan MOGUMOGU");
    mdns_txt_item_t txt[] = {{"proto", "1"}};
    err = mdns_service_add(nullptr, "_stackchan", "_tcp", _config.port, txt, 1);
    if (err != ESP_OK) {
        mclog::tagError(_tag, "mdns_service_add failed: {}", static_cast<int>(err));
        return;
    }
    _mdns_started = true;
}

void Server::stopMdns()
{
    bool started = false;
    {
        std::lock_guard<std::mutex> lock(_mutex);
        started = _mdns_started;
        _mdns_started = false;
    }
    if (started) {
        mdns_service_remove("_stackchan", "_tcp");
    }
}

}  // namespace mr_link
