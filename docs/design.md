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
- Quest が接続（認証成功）したら QR を消して顔に切り替える。切断したら QR に戻る（待受け）。Quest からの `showQr` や画面タッチでも再表示し、`hideQr` で消す。実装は `mr_link_qr.{h,cpp}`。
- 顔は公式の `DefaultAvatar`（目と口）を `GetStackChan()` に付けたもの。アプリを開くときに先に作り、その上に QR のページを全画面で重ねる。QR を消すと顔が見える。`emotion` メッセージはこの顔の表情を変える。顔の生成・破棄は `mr_link_face.{h,cpp}` の 1 か所にまとめてあり、見た目を変えるときはここだけ直す。

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
| `emotion` | `value`（下の表）、`durationMs`（省略時は 0） | 表情を変える。`durationMs` が正なら、その間だけ見せて、基本の表情へ戻す。0 以下なら、基本の表情として切り替える（タイマー中なら、終わってから） |
| `showQr` | なし | QR を再表示する（再キャリブ） |
| `look` | `yaw`（度、右が +）、`pitch`（度、上が +） | StackChan の顔の正面を 0 とした、ヘッドセットの方向。接続中、QR を隠している間だけ有効 |
| `hideQr` | なし | QR を消して顔に戻す。接続は切らない |

`emotion` の `value`:

| value | 顔 | 使う場面 |
|---|---|---|
| `neutral` | 通常 | 見るものがない（基本の表情） |
| `hungry` | 喜び顔＋よだれ | ご飯（球体）を追っている（基本の表情） |
| `needy` | 悲しい顔＋汗のしずく | プレイヤーを追っている。構ってほしい（基本の表情） |
| `satisfied` | もぐもぐ（口を動かす）→ 喜び顔＋ハート 2 つ＋ほお赤 | ご飯を食べた直後（5 秒） |
| `happy` | 喜び顔 | 接続した直後の挨拶（ファームが自分で出す） |
| `angry` `sad` `doubt` `sleepy` | 公式の表情そのまま | 今は使っていない |

Unity（`ExpressionUseCase`）が、状態が変わったときだけ基本の表情を送る。再接続のあとは、もう一度送る。食べた直後の `satisfied` は、`FeedSphereUseCase` が数秒の指定付きで送り、終わると基本の表情へ戻る。

StackChan → Quest:

| type | 項目 | 説明 |
|---|---|---|
| `hello` | `proto`（プロトコル版、現在 1）、`mac`（`AA:BB:CC:DD:EE:FF`）、`fw`（`V1.x` 形式） | 認証に成功した直後に 1 回送る |

例:

```
{"type":"emotion","value":"happy","durationMs":3000}
{"type":"showQr"}
{"type":"look","yaw":-12.5,"pitch":4.0}
{"type":"hideQr"}
{"type":"hello","proto":1,"mac":"AA:BB:CC:DD:EE:FF","fw":"V1.0.0"}
```

### look（首の追従）

- Quest は、顔の位置（QR で確定した姿勢）とヘッドセットの位置から、顔の正面を 0 とした yaw / pitch を計算して送る。顔の局所 +Z を正面、+Y を上とみなす（実機で要検証）。
- 送信は約 15 Hz。値は 0.1 度に丸める。Quest 側で時定数 0.15 秒のローパスをかける。頭が止まっていても同じ頻度で送り続ける（StackChan の無入力タイムアウトのため）。
- Quest が送るのは、接続中で、セットアップガイドが完了（Connected）しているときだけ。切断中、QR を表示している間（初回の接続前、再キャリブ中）は送らない。
- StackChan は、クライアントが接続していて QR を隠しているときだけ `look` を受け付け、それ以外は無視する。QR の表示中と切断中は首を正面に固定する（切断すると正面へ戻る）。
- 角度の単位変換: メッセージは「右が +」、サーボの yaw は「左が +」（0.1 度単位）なので、符号を反転して 10 倍する。yaw は ±120 度、pitch は 0〜80 度に収める（サーボ自身の可動範囲でもさらに制限される）。範囲外の値や非数は捨てる。
- StackChan 側でも目標値を平滑化し、サーボへの指令は最大 20 回/秒、0.3 度未満の変化は送らない。1 秒間 `look` が来なければ、その場で止める（`look` が再開したら追従も再開）。

### ファーム側の購読 API

`mr_link::GetServer()` から `addMessageHandler("emotion", ...)`（首は `NeckLock::attach()` が `look` を購読する）、`addConnectionHandler(...)`、`sendText(...)` を使う。ハンドラは HTTP サーバーのタスクで呼ばれるので、短く返し、LVGL を触るときは `LvglLockGuard` を取る。

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
4. 球体が出ている間、StackChan は顔（口）に一番近い球体の方を向く（手で持っている球体も対象）。複数あるときは、新しい球体が今の向く先より 10% かつ 3 cm 以上近いときだけ切り替える（行ったり来たりの防止）。食べて消えたら次に近い球体へ、なくなればプレイヤーへ戻る。

### 首の追従

接続中（QR を隠している間）は、StackChan が首を動かしてヘッドセットの方を向き続ける（球体が出ている間は、上記「遊ぶ」のとおり一番近い球体を向く）。プレイヤーが動けば首が追従する。切断したら正面に戻る。QR を表示している間は、QR の検出を安定させるため正面に固定する。

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
- UseCase: `CalibrateStackChanUseCase`（QR 検出 → 位置確定 → 保存）、`FeedSphereUseCase`（距離判定 → 消滅 → 感情送信）、`HeadLookUseCase`（顔と、一番近い球体またはヘッドセットの位置から yaw / pitch を計算して `look` を送る。平滑化と送信レート制限つき）、`LookTargetSelector`（向く先の選択。純粋計算）、インターフェース `IStackChanLink` / `IStackChanPoseProvider` / `IHeadPoseProvider` / `ISphereSpawner`
- Infrastructure: WebSocket 実装、アンカー永続化、Null 実装（エディタ用）
- Platform.Meta: QR 検出、Passthrough 設定、ヘッドセット位置（`OvrHeadPoseProvider`、`OVRCameraRig` の centerEye）
- DebugTools: StackChan 無しで動かすための疑似 Link と疑似位置

## ファームウェアの構成

`firmware/overlay/main/apps/app_mr_link/` に自作アプリを置く。`mr_link_neck.{h,cpp}`（`NeckLock`）が首を持つ。MOGUMOGU を開いている間は `Motion::setModifyLock(true)` で `idle_motion` などを無効にする。QR 表示中と切断中は正面に固定し、接続中で QR を隠している間は `look` を受けて首を追従させる（上記「look」）。閉じるときに元のロック状態へ戻す。ビルド方法は [firmware/README.md](../firmware/README.md)。

## StackChan の座標系

StackChan がヘッドセットやご飯を見る向きは、すべて Unity が計算する。ファームは、受け取った角度を動かすだけにする（方向の計算をしない）。向きがずれたときに、「Unity の前提が悪いのか」を切り分けられるよう、StackChan の座標系をシーンの中に明示する。

- **原点（`Pivot`）:** 首の上下の軸（ピッチ軸）の位置。縦の首の軸（ヨー軸）はこの点を通ると考える。QR の中心（= 顔の中心）からのオフセット（`PivotLocalOffset`、QR の向きで測る）で決める。
- **向き（`Rotation`）:** サーボが yaw 0 度、pitch 0 度のときの向き。QR の向き（+Z が顔の正面、+Y が上と仮定）に、`YawZeroOffsetDeg`（右が +）と `PitchZeroOffsetDeg`（上が +）を加えたもの。
- **角度:** 対象の位置を、この座標系で見たときの方向。yaw は右が +、pitch は上が +。`InvertYaw` / `InvertPitch` で符号を反転できる。この角度がそのまま `look` で送られる。
- 設定は `StackChanRigConfig`（ScriptableObject）。`MrSceneLifetimeScope` の `Rig Config` に割り当てる（空なら初期値）。初期値は、オフセット 0、補正 0、反転なし。これは、顔の中心から見た方向を、そのまま送る従来の動きと同じ。
- 計算は `StackChanFrame`（UseCase、純粋な計算）。シーンには `StackChan` という GameObject があり、位置と向きがこの座標系に合っている（顔の位置が分かっている間）。エディタと開発ビルド（`RSC_DEVELOPMENT`）では、この上に軸（X 赤、Y 緑、Z 青）、首の軸の点（白い球）、見ている方向（黄色の線）を表示する。

### 実測と調整

次を測って `StackChanRigConfig` に入れる。表示を見ながら、実機とずれていないか確かめる。

1. **`PivotLocalOffset`:** QR の中心から、首の軸までの位置。Z は、顔の正面（QR の面の法線）方向の距離。顔より後ろなら負（QR の +Z が正面だと仮定）。Y は、QR の中心から、頭の中の上下に振る軸までの高さ。X は左右のずれ。
2. **`YawZeroOffsetDeg` / `PitchZeroOffsetDeg`:** 首を正面（サーボ 0 度）にしたとき、QR の法線と、顔が実際に向いている方向のずれ。
3. **`InvertYaw` / `InvertPitch`:** 首が逆に回るときに反転する。ファームの `kYawSign`（仮の補正、現在 +1）と二重にならないよう、調整が済んだら、ファームの `kYawSign` を外して、角度をそのまま使うようにする（ファームの書き込みと Quest アプリの再ビルドを同時に行う）。

近距離（ご飯）のずれは、主にオフセット（1）の誤りで、遠距離（プレイヤー）のずれは、主に補正（2）の誤りで出る。

## リスクと要検証

- QR 検出 API（調査済み）: Meta XR SDK 207.0.1 の MRUK に QR トラッキングがある（`MRUK.SceneSettings.TrackerConfiguration.QRCodeTrackingEnabled`、`MRUKTrackable.MarkerPayloadString`、`MRUK.QRCodeTrackingSupported`）。これを採用し、Passthrough Camera API + ZXing は不要（`MrukQrStackChanPoseProvider`）。
  - 要件: Quest 3 / 3S（`QRCodeTrackingSupported` で確認）、Scene 権限（`OVRPermissionsRequester.ScenePermission`）。MR シーンに `MRUK` と `OVRCameraRig` が必要。Passthrough Camera Access（`HEADSET_CAMERA` 権限）は不要。必要な Quest OS の最低バージョンは SDK のソースから分からないので要確認。
  - 要検証（実機）: QR の姿勢の軸（面の法線が +Z か）と、`Trackable` の原点が QR の中心か。口のオフセット（`EatConfig.MouthLocalOffset`、現在は仮値）。検出の更新頻度（MRUK のトラッカーは約 1Hz）と距離の限界。
  - MRUK を置くと `LoadSceneOnStartup` で部屋スキャンを要求するため、不要なら MRUK の `Load Scene On Startup` を切る。
  - MRUK が無いシーン（エディタなど）では Null 実装にフォールバックする。
- QR が小さい: 2 インチ画面なので、検出距離は 1m 程度が限界。画面の反射にも注意する。初回だけ近づけば足りる。
- Wi-Fi: 端末間通信を遮断する設定（ゲストネットワークなど）では接続できない。
- 首の動き（#26）: 接続中は首がヘッドセットを追うので、顔と口の位置が QR で確定した位置からずれる。食べる判定の補正は未実装。次のどれにするか実機で決める。
  - Quest が自分で送った角度から首の回転を打ち消して補正する（首の回転軸と口のオフセットを実測する）。
  - StackChan が現在の首の角度を Quest に送り、Quest が補正する。
  - ずれが小さければ補正しない。
  - あわせて要検証（実機）: yaw / pitch の符号（右が +、上が +）と、QR の向き（面の +Z が StackChan の正面か）の対応、追従の遅れとサーボ音。
- ファームの書込み: ESP-IDF 5.5.x の環境が必要。
