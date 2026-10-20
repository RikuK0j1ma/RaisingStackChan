# 設計

## ゴール

Quest の MR（パススルー）で、StackChan のいる場所に球体（餌）を出す。球体が StackChan の口に十分近づいたら、球体が消えて StackChan が喜び顔になる。

## 全体構成

```
[StackChan]  アプリ MR LINK（公式ファームに追加）
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

## 通信

StackChan が WebSocket サーバー、Quest がクライアント。素の JSON テキストを使う。

- Quest → StackChan: `{"type":"emotion","value":"happy","durationMs":3000}`、`{"type":"showQr"}`
- StackChan → Quest: `{"type":"hello","mac":"...","fw":"..."}`

公式ファームの既存の LAN WebSocket（`hal/utils/local_network`）は、StackChan がクライアントで、接続先をクラウド経由で受け取る作りなので使わない。

## ユーザー体験

### 初回セットアップ

1. StackChan の電源を入れる。Wi-Fi が未設定なら、公式の BLE 設定（StackChan World アプリ）で設定する。Quest と同じ Wi-Fi にする。
2. StackChan のランチャーで MR LINK を開く。顔の代わりに QR と案内を表示する。
3. Quest でアプリを起動する。パススルーが表示される。初回だけ権限ダイアログ（カメラ、空間データ、アンカー）が出る。
4. 「StackChan の顔を見てください」の案内が出る。QR を見る。
5. QR を検出すると、顔の位置にリングが出て WebSocket が接続される。StackChan は QR を消して顔に戻り、一瞬ニコッとして接続完了を知らせる。位置は Spatial Anchor に保存する。

### 2回目以降

アプリ起動 → アンカー復元 → 前回の接続先または mDNS で自動再接続。QR は不要。

### 遊ぶ

1. 手で球体を出し、StackChan の顔へ持っていく。
2. 口付近の半径内に一定時間入ると、球体が口へ吸い込まれて縮み、効果音とともに消える。
3. StackChan が喜び顔（Happy）になり、数秒後に通常顔へ戻る。

### 位置がずれたとき

Quest のメニューから「再キャリブ」。StackChan が QR を再表示する。

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

- QR 検出 API: Meta XR SDK 207 の MRUK に QR トラッキングがあるか要確認。無ければ Passthrough Camera API と ZXing で自前検出する。どちらも Quest 3 / 3S 限定。
- QR が小さい: 2 インチ画面なので、検出距離は 1m 程度が限界。画面の反射にも注意する。初回だけ近づけば足りる。
- Wi-Fi: 端末間通信を遮断する設定（ゲストネットワークなど）では接続できない。
- 首の動き: MR 中は固定が前提。動かすなら、サーボ角を Quest に送って補正する。
- ファームの書込み: ESP-IDF 5.5.x の環境が必要。
