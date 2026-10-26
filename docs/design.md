# 設計

## ゴール

Quest の MR（パススルー）で、StackChan のいる場所に球体（餌）を出す。球体が StackChan の口に十分近づいたら、球体が消えて StackChan が喜び顔になる。

## 全体構成

```
[StackChan]  アプリ MOGUMOGU（公式ファームに追加）
  ├ QR 表示（接続情報 + 位置マーカー）
  ├ WebSocket サーバー + mDNS
  └ 受信 → emotion → Avatar::setEmotion(Happy) → 数秒後に Neutral
        ↑ 同一 Wi-Fi
[Quest 3 / 3S]  Unity MR アプリ
  ├ QR 検出 → 顔の位置・向きを確定 → Spatial Anchor に保存
  ├ 球体の生成・掴み・移動
  └ 口の半径内に一定時間 → 球体消滅 → emotion 送信
```

QR は「接続先（IP・ポート・トークン）」と「顔の位置」を一度に渡す。ペアリングと位置合わせが同じ操作になる。

### QR のペイロード

QR の文字列は接続 URL そのもの。Unity 側のパーサーと一致させている。

```
ws://<ip>:8765/ws?token=<token>
```

- `<ip>` は StackChan の Wi-Fi（STA）の IPv4。`<token>` は 16 桁の 16 進文字列。全体で約 55 文字（QR バージョン 4 前後）。
- 当初案の `rsc://<ip>:<port>?t=<token>` は採らない。URL をそのまま接続に使えるので、変換が要らない。
- 画面（320x240）に、黒地に白の QR（176px、周囲に白い余白）を中央寄せで全画面表示する。上部に案内テキスト（英語。標準フォントに日本語の字が無いため「Questで見てね」は出せない）。
- Wi-Fi が未接続（IP が無い）ときは QR の代わりに案内（BLE で StackChan World アプリから Wi-Fi を設定して MOGUMOGU を開き直す）を出す。IP が後から取れたら自動で QR に切り替わる。
- Quest が接続（認証成功）したら QR を消して顔に戻る。Quest からの `showQr` で再表示し、`hideQr` で消す。実装は `mr_link_qr.{h,cpp}`。

## 通信

StackChan が WebSocket サーバー、Quest がクライアント。素の JSON テキスト（テキストフレーム、1 メッセージ 1024 バイト以下）を使う。実装は `firmware/overlay/main/apps/app_mr_link/mr_link_server.{h,cpp}`。

### 接続

- URL: `ws://<host>:8765/ws?token=<token>`。`<host>` は QR で受け取った IP、または mDNS の `stackchan.local`。
- トークンは StackChan の起動ごとに生成する 16 桁の 16 進文字列。QR で Quest へ渡す。NVS には永続化しない。
  - 再起動するとトークンが変わるので、Quest が保存した前回の URL では接続できない（認証で切断される）。この場合は QR を読み直す。MOGUMOGU を開くと必ず QR が出るので、それが再接続の手順になる（再キャリブと同じ操作）。
  - 同じ起動中は、MOGUMOGU を閉じて開き直してもトークンは同じ。Quest が落ちて戻っただけなら、保存した URL でそのまま再接続できる。
  - Quest 側は、保存済み URL での接続が認証で切断されたら「QR を読んでください」へ戻すこと。
- トークンが誤りなら、接続直後に StackChan が切断する（WebSocket のハンドシェイク完了後に検証するため、HTTP エラーではなく切断で知る）。切断後の `message` は一切処理しない。
- 接続は 1 クライアントのみ。認証済みの新しい接続が来たら、古い接続を切って置き換える（Quest が落ちて半開きになった接続で塞がらないため）。
- 切断は TCP keep-alive（約 20 秒）と WebSocket の close で検知する。切断後は待受けに戻る。
- mDNS: ホスト名 `stackchan.local`、サービス `_stackchan._tcp`（TXT `proto=1`）。端末によっては `.local` を解決できないので、IP（QR 由来）にフォールバックできるようにする。

### メッセージ

共通: `type`（文字列）が必須。未知の `type`、`type` の無い JSON、不正な JSON は無視する（切断しない）。

Quest → StackChan:

| type | 項目 | 説明 |
|---|---|---|
| `emotion` | `value`（`happy` ほか）、`durationMs`（省略時は StackChan 側の既定） | 表情を変える。`durationMs` 後に Neutral へ戻す |
| `showQr` | なし | QR を再表示する（再キャリブ） |
| `hideQr` | なし | QR を消して顔に戻す。接続は切らない |

StackChan → Quest:

| type | 項目 | 説明 |
|---|---|---|
| `hello` | `proto`（プロトコル版、現在 1）、`mac`（`AA:BB:CC:DD:EE:FF`）、`fw`（`V1.x` 形式） | 認証に成功した直後に 1 回送る |

例:

```
{"type":"emotion","value":"happy","durationMs":3000}
{"type":"showQr"}
{"type":"hideQr"}
{"type":"hello","proto":1,"mac":"AA:BB:CC:DD:EE:FF","fw":"V1.0.0"}
```

### ファーム側の購読 API

`mr_link::GetServer()` から `addMessageHandler("emotion", ...)`、`addConnectionHandler(...)`、`sendText(...)` を使う。ハンドラは HTTP サーバーのタスクで呼ばれるので、短く返し、LVGL を触るときは `LvglLockGuard` を取る。

公式ファームの既存の LAN WebSocket（`hal/utils/local_network`）は、StackChan がクライアントで、接続先をクラウド経由で受け取る作りなので使わない。

## ユーザー体験

### 初回セットアップ

1. StackChan の電源を入れる。Wi-Fi が未設定なら、公式の BLE 設定（StackChan World アプリ）で設定する。Quest と同じ Wi-Fi にする。
2. StackChan のランチャーで MOGUMOGU を開く。顔の代わりに QR と案内を表示する。
3. Quest でアプリを起動する。パススルーが表示される。初回だけ権限ダイアログ（カメラ、空間データ、アンカー）が出る。
4. 「StackChan の顔を見てください」の案内が出る。QR を見る。
5. QR を検出すると、顔の位置にリングが出て WebSocket が接続される。StackChan は QR を消して顔に戻り、一瞬ニコッとして接続完了を知らせる。位置は Spatial Anchor に保存する。

### 2回目以降

アプリ起動 → アンカー復元 → 前回の接続先または mDNS で自動再接続。StackChan を再起動していなければ QR は不要（再起動後はトークンが変わるので QR を読み直す）。

### 遊ぶ

1. 手で球体を出し、StackChan の顔へ持っていく。
2. 口付近の半径内に一定時間入ると、球体が口へ吸い込まれて縮み、効果音とともに消える。
3. StackChan が喜び顔（Happy）になり、数秒後に通常顔へ戻る。

### 位置がずれたとき

Quest の左コントローラーの **Y ボタン**で、StackChan の QR と顔を切り替える（押すたびに「顔 → QR → 顔」）。メニューの「再キャリブ」でも QR が出る。

| 状態 | Y ボタン | 結果 |
|---|---|---|
| 顔が出ている（接続中） | 押す | `showQr` を送る。StackChan に QR が出る。保存済みのアンカーは、新しい位置が確定するまで残す |
| QR が出ている | 押す | `hideQr` を送る。顔に戻る。位置は今のまま（アンカーも更新しない） |
| QR が出ている | 新しい QR を読み取れた | アンカーを更新して保存し、`hideQr` を送って顔に戻る（Y を押さなくてよい） |

- どの場合も WebSocket は切らない。接続していないときの Y ボタンは無視する。
- StackChan は新しく接続されたときも QR を消す。Quest 側は接続を検知したら「QR を表示中」の状態を捨てる。
- 実装: 入力は Platform.Meta の `OvrQrToggleInput`（`OVRInput.Button.Four`、左コントローラー）、状態管理は UseCase の `RecalibrationService`（`IsQrShown`、`ToggleQrAsync`）。ファームは `hideQr` のハンドラが `_qr.hide()` を呼ぶ。

## Unity の構成

層構造は [CLAUDE.md](../CLAUDE.md) を参照。

- Entity: `EatConfig`（食べる半径、保持時間）、`StackChanEmotion`
- UseCase: `CalibrateStackChanUseCase`（QR 検出 → 位置確定 → 保存）、`FeedSphereUseCase`（距離判定 → 消滅 → 感情送信）、インターフェース `IStackChanLink` / `IStackChanPoseProvider` / `ISphereSpawner`
- Infrastructure: WebSocket 実装、アンカー永続化、Null 実装（エディタ用）
- Platform.Meta: QR 検出、Passthrough 設定
- DebugTools: StackChan 無しで動かすための疑似 Link と疑似位置

## ファームウェアの構成

`firmware/overlay/main/apps/app_mr_link/` に自作アプリを置く。MR 中は `idle_motion` を無効にして首を固定する。ビルド方法は [firmware/README.md](../firmware/README.md)。

## リスクと要検証

- QR 検出 API（調査済み）: Meta XR SDK 207.0.1 の MRUK に QR トラッキングがある（`MRUK.SceneSettings.TrackerConfiguration.QRCodeTrackingEnabled`、`MRUKTrackable.MarkerPayloadString`、`MRUK.QRCodeTrackingSupported`）。これを採用し、Passthrough Camera API + ZXing は不要（`MrukQrStackChanPoseProvider`）。
  - 要件: Quest 3 / 3S（`QRCodeTrackingSupported` で確認）、Scene 権限（`OVRPermissionsRequester.ScenePermission`）。MR シーンに `MRUK` と `OVRCameraRig` が必要。Passthrough Camera Access（`HEADSET_CAMERA` 権限）は不要。必要な Quest OS の最低バージョンは SDK のソースから分からないので要確認。
  - 要検証（実機）: QR の姿勢の軸（面の法線が +Z か）と、`Trackable` の原点が QR の中心か。口のオフセット（`EatConfig.MouthLocalOffset`、現在は仮値）。検出の更新頻度（MRUK のトラッカーは約 1Hz）と距離の限界。
  - MRUK を置くと `LoadSceneOnStartup` で部屋スキャンを要求するため、不要なら MRUK の `Load Scene On Startup` を切る。
  - MRUK が無いシーン（エディタなど）では Null 実装にフォールバックする。
- QR が小さい: 2 インチ画面なので、検出距離は 1m 程度が限界。画面の反射にも注意する。初回だけ近づけば足りる。
- Wi-Fi: 端末間通信を遮断する設定（ゲストネットワークなど）では接続できない。
- 首の動き: MR 中は固定が前提。動かすなら、サーボ角を Quest に送って補正する。
- ファームの書込み: ESP-IDF 5.5.x の環境が必要。
