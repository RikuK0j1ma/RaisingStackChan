# RaisingStackChan

Meta Quest の MR（パススルー）で、ロボット **StackChan** に漫画みたいな骨付き肉を食べさせて遊ぶアプリです。

- Quest の目の前の世界に、骨付き肉が出てきます。
- 骨を持って、StackChan の口に近づけます。
- StackChan は肉に気づいて顔を向け、ひと口ずつかじります。かじられた所には断面が見えて、肉が少しずつ減っていきます。
- 食べるたびに、StackChan が満足した顔になります。

Quest（Unity で作ったアプリ）と StackChan（自作アプリ `MOGUMOGU` を足したファームウェア）は、同じ Wi-Fi の中で通信します。

---

## このリポジトリの中身

```
unity/       Quest 用の MR アプリ（Unity のプロジェクト）
firmware/    StackChan のファームウェア（公式 + 自作アプリ MOGUMOGU）
docs/        設計や手順書
```

| 読みたいこと | ここを見る |
|---|---|
| ファームウェアのビルドと書き込み（初心者向けの手順） | [docs/firmware-build.md](docs/firmware-build.md) |
| 仕組みと遊び方の設計 | [docs/design.md](docs/design.md) |
| ファームウェアの構成 | [firmware/README.md](firmware/README.md) |
| Unity のコードの置き方・規約 | [CLAUDE.md](CLAUDE.md) |

---

## 必要なもの

### 機材

- **Meta Quest 3 または 3S**（開発者モードを有効にしたもの）
- **StackChan**（M5Stack の CoreS3 を使った公式のロボット）と、PC につなぐ USB ケーブル
- 上の 2 つを同じ Wi-Fi につなげられる環境
- Windows の PC

### ソフト（PC に入れる）

| ソフト | 用途 | 入れ方 |
|---|---|---|
| Git と Git LFS | このリポジトリの取得 | https://git-scm.com/ と https://git-lfs.com/ |
| Unity Hub と Unity **6000.4.1f1** | Quest アプリを作る | Unity Hub から。Android Build Support（OpenJDK、SDK、NDK を含む）も選ぶ |
| ESP-IDF **5.5.x** | StackChan のファームウェアを作る | https://dl.espressif.com/dl/esp-idf/ の Windows インストーラー。詳しくは [docs/firmware-build.md](docs/firmware-build.md) |

> Unity のバージョンは、必ず **6000.4.1f1** に合わせてください。違うバージョンで開くと、プロジェクトが書き換わることがあります。

---

## 手順

大きく 3 つです。

1. リポジトリを取得する
2. StackChan にファームウェアを書き込む
3. Quest にアプリを入れて遊ぶ

### 1. リポジトリを取得する

Git LFS は、画像やモデルなどの大きなファイルを取得するのに必要です。最初に 1 回だけ有効にします。

```powershell
git lfs install
git clone --recurse-submodules https://github.com/RikuK0j1ma/RaisingStackChan.git
```

`--recurse-submodules` を付け忘れたときは、clone したフォルダの中で次を実行します。

```powershell
git submodule update --init
```

> `firmware/upstream` は M5Stack の公式リポジトリです。**中のファイルは編集しないでください。**

### 2. StackChan にファームウェアを書き込む

詳しい手順は [docs/firmware-build.md](docs/firmware-build.md) にあります。ここでは流れだけ説明します。

1. ESP-IDF を入れる（初回のみ）。
2. ESP-IDF の PowerShell を開いて、`firmware` フォルダで `.\build.ps1` を実行する。初回は部品をダウンロードするので、数分から数十分かかります。
3. StackChan を USB ケーブルで PC につなぎ、COM ポートの番号を調べる。
4. 次のコマンドで書き込む（`COM5` は自分のポートに置き換える）。

   ```powershell
   .\build.ps1 -Task flash -Port COM5
   ```

> **注意**: 書き込むと、StackChan に入っていた工場出荷のファームウェアは置き換わります。元に戻したいときは、M5Stack 公式の手順で書き直します。

StackChan が Wi-Fi につながっていないときは、公式のスマートフォンアプリ（StackChan World）で Wi-Fi を設定します。**Quest と同じ Wi-Fi** にしてください。

### 3. Quest にアプリを入れて遊ぶ

#### Unity でプロジェクトを開く

1. Unity Hub で、リポジトリの `unity` フォルダを「プロジェクトを開く」から追加する。
2. 初回は、パッケージの取得と読み込みで時間がかかります。そのまま待ちます。
3. `Assets/__RikuK0j1ma__/__RaisingStackChan__/Scene/MR.unity` を開く。これがアプリ本体のシーンです。

#### Quest にビルドして入れる

1. Quest を USB ケーブルで PC につなぎ、Quest 側で USB デバッグを許可する。
2. Unity のメニュー **File → Build Profiles** で **Android** を選び、**Switch Platform** を押す（初回のみ）。
3. **Run Device** に Quest が出ていることを確認して、**Build And Run** を押す。
4. ビルドが終わると、Quest でアプリが起動します。

> Quest の開発者モードの有効化は、Meta Horizon アプリの「デバイス」設定からできます。
> Unity で Meta の設定の警告が出たときは、メニューの **Meta XR → Project Setup Tool**（または **Edit → Project Settings → Meta XR**）を開き、案内に従って直します。

#### 遊ぶ

1. StackChan の電源を入れる。
2. StackChan の画面で **MOGUMOGU** を開く。顔の代わりに QR コードが出ます。
3. Quest でアプリを起動する。初回だけ、カメラや空間データの権限を聞かれるので、許可する。
4. 「StackChan の顔を見てください」と出るので、QR を見る。
5. QR を読み取ると、Quest と StackChan がつながり、StackChan の画面が顔に戻ります。顔の位置は Quest が覚えます。
6. 次の操作で遊びます。

| やりたいこと | コントローラー | 手（ハンドトラッキング） |
|---|---|---|
| 肉を出す | A ボタン（右）/ X ボタン（左） | 中指のピンチ |
| 骨を持つ | グリップ | 人差し指のピンチ |

持てるのは**骨だけ**です。肉は骨にくっついて動きます。肉を StackChan の口の近くに、少しのあいだ置いてください。ひと口かじってくれます。決まった回数かじると、肉は全部なくなります。

2 回目以降は、StackChan を再起動していなければ、QR を読み直さなくても自動でつながります。StackChan を再起動したときは、QR を読み直します。

---

## うまくいかないとき

| 症状 | 確認すること |
|---|---|
| Quest と StackChan がつながらない | 同じ Wi-Fi か。StackChan で MOGUMOGU が開いて QR が出ているか。StackChan を再起動したあとは、QR を読み直したか |
| QR を読み取れない | 明るい場所で、QR を正面から、少し近づけて見る |
| 肉が StackChan に食べられない | 肉（骨ではなく肉の部分）が、口の近くにあるか。少し待つ |
| Unity を開いたら、画像やモデルが真っ白・壊れている | `git lfs install` をしたか。`git lfs pull` を実行する |
| `firmware/upstream` が空 | `git submodule update --init` を実行する |
| ファームウェアのビルドが失敗する | [docs/firmware-build.md](docs/firmware-build.md) の「困ったとき」を見る |

---

## 開発者向け

### ヘッドセットなしで試す

Unity のエディタで再生すると、ダミーの顔と簡易な食べ物が出る開発用の仕組み（DebugTools）が動きます。開発用のシンボル `RSC_DEVELOPMENT` が有効なときだけ使えます。

### かじった跡の見た目を試す

Quest か、Quest Link でつないだエディタで再生中に、ヒエラルキーの `Foods` を選びます。インスペクターの `Manga Meat Bite Tester` の︙メニューから、次を呼べます。

- `Spawn Meat In Front Of Camera`: カメラの前に肉を出す
- `Bite Once`: 肉をひと口かじる

### 調整できる値

`Assets/__RikuK0j1ma__/__RaisingStackChan__/Data/SphereConfig.asset` で、骨の長さ、ひと口の大きさ、かじる回数、骨を持てる距離などを変えられます。

### テスト

Unity のメニュー **Window → General → Test Runner** を開き、**EditMode** のテストを実行します。

### 使っている主なもの

| 名前 | バージョン |
|---|---|
| Unity | 6000.4.1f1 |
| Universal RP | 17.4.0 |
| Meta XR All-in-One SDK | 207.0.1 |
| OpenXR Plugin | 1.16.1 |
| VContainer | 1.19.0 |
| UniTask | 2.5.11 |
| MessagePipe (+ VContainer) | 1.8.2 |
| ESP-IDF | 5.5.x |

そのほかのパッケージは `unity/Packages/manifest.json` にあります。

### 設計を読むには

まず [docs/design.md](docs/design.md) の「ゴール」と「全体構成」を読むと、全体がつかめます。コードを書くときの決まりは [CLAUDE.md](CLAUDE.md) にあります。
