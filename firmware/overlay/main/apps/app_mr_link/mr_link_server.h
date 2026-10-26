/*
 * SPDX-License-Identifier: MIT
 */
#pragma once
#include <ArduinoJson.hpp>
#include <cstdint>
#include <functional>
#include <mutex>
#include <string>
#include <vector>
#include <esp_http_server.h>

namespace mr_link {

/**
 * @brief Protocol version reported in the "hello" message.
 */
inline constexpr int kProtocolVersion = 1;

/**
 * @brief WebSocket server for the Quest app (plain JSON text frames).
 *
 * - Listens on Config::port, path "/ws". The client must pass "?token=<token>" in the URL.
 * - Publishes <hostname>.local (and a "_stackchan._tcp" service) over mDNS.
 * - Only one client at a time. A newly authenticated client replaces the previous one.
 * - Unknown message types are ignored.
 *
 * Handlers run on the HTTP server task, not on the LVGL/main task. They must return quickly,
 * and must take LvglLockGuard before touching LVGL.
 */
class Server {
public:
    struct Config {
        uint16_t port = 8765;
        std::string hostname = "stackchan";  // mDNS host name -> "stackchan.local"
        std::string token;                   // required, non-empty
    };

    /** Called with the parsed message. doc["type"] is always a string. */
    using MessageHandler = std::function<void(const ArduinoJson::JsonDocument& doc)>;
    /** Called with true when a client is authenticated, false when it is gone. */
    using ConnectionHandler = std::function<void(bool connected)>;

    Server() = default;
    ~Server();
    Server(const Server&) = delete;
    Server& operator=(const Server&) = delete;

    /** Start the HTTP/WebSocket server and mDNS. Returns false on failure. No-op if already started. */
    bool start(const Config& config);
    /** Stop everything and drop the client. */
    void stop();
    bool isRunning() const;
    bool hasClient() const;

    /**
     * @brief Subscribe to messages of one type, e.g. "emotion" or "showQr".
     * @return id for removeHandler(). Handlers survive stop()/start().
     */
    int addMessageHandler(const std::string& type, MessageHandler handler);
    /** Subscribe to connect / disconnect events. @return id for removeHandler(). */
    int addConnectionHandler(ConnectionHandler handler);
    void removeHandler(int id);

    /** Send a text frame to the connected client. Returns false if there is no client or sending failed. */
    bool sendText(const std::string& json);

    /** Random token (16 hex chars) for the QR code. */
    static std::string generateToken();

private:
    struct MessageEntry {
        int id;
        std::string type;
        MessageHandler handler;
    };
    struct ConnectionEntry {
        int id;
        ConnectionHandler handler;
    };

    static esp_err_t wsHandler(httpd_req_t* req);
    static esp_err_t onHandshakeDone(httpd_req_t* req);
    esp_err_t handleHandshake(httpd_req_t* req);
    static void onSessionClose(httpd_handle_t hd, int sockfd);

    esp_err_t handleRequest(httpd_req_t* req);
    bool checkToken(httpd_req_t* req) const;
    void dispatch(const char* payload, size_t len);
    void notifyConnection(bool connected);
    void sendHello(int fd);
    void startMdns();
    void stopMdns();

    mutable std::mutex _mutex;  // guards the fields below
    Config _config;
    httpd_handle_t _server = nullptr;
    int _client_fd = -1;
    bool _mdns_started = false;
    int _next_id = 1;
    std::vector<MessageEntry> _message_handlers;
    std::vector<ConnectionEntry> _connection_handlers;
};

/** The single server instance, shared by the app and the features built on top of it. */
Server& GetServer();

}  // namespace mr_link
