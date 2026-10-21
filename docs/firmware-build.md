# StackChan ファームウェアのビルドと書き込み（初心者向け）

StackChan の本体に、自作アプリ `MR LINK` を入れた**ファームウェア**を作って書き込むまでの手順です。
プログラミングやコマンド操作に慣れていなくても進められるように、1 ステップずつ書いています。

> 注意: この手順書は、`firmware/build.ps1` を ESP-IDF の環境がある PC で動かした実績がない状態で書いています（`firmware/README.md` の「現状」参照）。
> 途中でエラーが出たら、「困ったとき」の節を見てください。解決しなければエラー文をそのまま共有してください。

---

## 0. 全体の流れ

1. 必要なソフトを PC に入れる（初回のみ）
2. このリポジトリを取得する（初回のみ）
3. ビルドする（ファームウェアのファイルを作る）
4. StackChan を PC につなぎ、書き込む
5. 動作を確認する

用語:

- **ファームウェア**: StackChan 本体の中で動いているプログラム。
- **ビルド**: ソースコードを、本体が実行できる形に変換すること。
- **書き込み（フラッシュ）**: ビルドしたファームウェアを本体に入れること。
- **ESP-IDF**: StackChan の頭脳（ESP32-S3）用の公式開発キット。ビルドに必要。
- **MR LINK**: このプロジェクトで作った StackChan 用アプリ。Quest から Wi-Fi でつながる。

---

## 1. 事前に知っておくこと

### 書き込むと、工場出荷のファームウェアは置き換わる

`flash` を実行すると、本体に入っていたファームウェアが**上書き**されます。
今回ビルドするのは「公式ファームウェア + MR LINK」なので、公式の機能（おしゃべりなど）は基本的に残りますが、以下は了承した上で進めてください。

- 書き込み途中にケーブルを抜くと、起動しなくなることがあります（後述の方法で復旧できる場合が多い）。
- 元の状態に戻したい場合は、M5Stack 公式の手順で公式ファームウェアを書き直します（M5Burner など）。

### 必要なもの

- Windows の PC（空き容量 **10GB 以上**を推奨。ESP-IDF と依存ライブラリが大きい）
- インターネット接続（初回は数分〜数十分かかる）
- StackChan 本体
- データ通信に対応した USB-C ケーブル（充電専用ケーブルだと PC が認識しない）
- このリポジトリ（`RaisingStackChan`）
- Git と Git LFS、Python（ESP-IDF のインストーラーが入れてくれるものもあります）

---

## 2. 初回の準備

### 2-1. ESP-IDF をインストールする

このプロジェクトは **ESP-IDF 5.5.x** を使います（`firmware/build.ps1` の要件）。

1. Espressif 公式の「ESP-IDF Windows Installer」をダウンロードします。
   - 検索: `ESP-IDF Windows Installer` → Espressif の公式ドキュメントのページから入手
2. インストーラーで **バージョン 5.5.x** を選びます（5.4 以前や 6.x は選ばない）。
3. そのまま進めてインストールを完了します。
4. 終わると、スタートメニューに **「ESP-IDF 5.5 PowerShell」**（名前は多少違うことがあります）が追加されます。**ビルドはこのシェルで行います。**

> 普通の PowerShell では `idf.py` が使えません。必ず ESP-IDF 用のシェルを使ってください。

### 2-2. Git LFS を入れる

このリポジトリは Git LFS を使っています。入っていない場合は、通常の PowerShell で次を実行します。

```powershell
git lfs install
```

（`git lfs` が見つからないと言われたら、Git LFS をインストールしてからやり直します。）

### 2-3. リポジトリを取得する

すでに手元にある場合は、「2-4」へ進みます。

```powershell
git clone https://github.com/RikuK0j1ma/RaisingStackChan.git
cd RaisingStackChan
```

### 2-4. 公式ファームウェア（サブモジュール）を取得する

`firmware/upstream` には、M5Stack 公式のファームウェアが入ります。最初は空なので、取得します。

```powershell
git submodule update --init
```

> `firmware/upstream` の中身は**絶対に編集しません**。自作のコードは `firmware/overlay/` に置きます。

---

## 3. ビルドする

1. スタートメニューから **ESP-IDF 5.5 PowerShell** を開きます。
2. リポジトリの `firmware` フォルダへ移動します（パスは自分の環境に合わせます）。

   ```powershell
   cd D:\RikuKojima\RaisingStackChan\firmware
   ```

3. ビルドスクリプトを実行します。

   ```powershell
   .\build.ps1
   ```

   「スクリプトの実行が無効」というエラーが出た場合は、そのシェルだけ許可して再実行します。

   ```powershell
   Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
   .\build.ps1
   ```

### 何が起きているか

`build.ps1` が次のことを自動でやります。

1. 公式ファームウェアを `firmware/.work/` にコピーする（公式の元ファイルは触らない）
2. 自作アプリ `MR LINK`（`firmware/overlay/`）を重ねる
3. アプリを登録する
4. 依存ライブラリを取得する（**初回は数分〜数十分かかります**）
5. `idf.py build` でビルドする

### 成功の目安

最後のほうに、次のような表示が出ればビルド成功です。

```
Project build complete.
```

（アプリのサイズ表示や、書き込みコマンドの案内が出ることもあります。）
成功しても、まだ本体には何も入っていません。次の手順で書き込みます。

### ビルドだけ試したい／準備だけしたいとき

```powershell
.\build.ps1 -Task prepare    # ビルドはせず、準備（コピーと登録）だけ行う
```

---

## 4. StackChan に書き込む

### 4-1. 本体を PC につなぐ

1. StackChan の電源を入れたまま、データ通信対応の USB-C ケーブルで PC とつなぎます。
2. **ポート番号（COM○）を調べます。** ESP-IDF のシェルで次を実行します。

   ```powershell
   Get-CimInstance Win32_SerialPort | Select-Object DeviceID, Description
   ```

   `COM5` のような名前が出ます。ケーブルを抜き差しして、増減するものが StackChan です。
   （「デバイス マネージャー」→「ポート (COM と LPT)」でも確認できます。）

> COM ポートが出ない場合は、ケーブルを変える、別の USB ポートに挿す、USB ドライバーを入れる（後述）を試してください。

### 4-2. 書き込む

`COM5` は 4-1 で調べた番号に置き換えます。

```powershell
.\build.ps1 -Task flash -Port COM5
```

- 書き込み中は**ケーブルを抜かない、PC をスリープさせない**でください。
- 終わると本体が再起動します。ログに `Hard resetting via RTS pin...` と出れば書き込み完了です。

### 書き込みに失敗するとき（接続できない）

`Failed to connect to ESP32-S3` のようなエラーが出る場合は、本体を**ダウンロードモード**にします。

1. 本体のリセットボタンを、ライトが光るまで**長押し**します（機種により数秒）。
2. その状態のまま、もう一度 `.\build.ps1 -Task flash -Port COM5` を実行します。

> ダウンロードモードの操作は機種・個体で異なることがあります。ボタンの位置や操作は M5Stack 公式のドキュメントで確認してください。

---

## 5. 動作を確認する

### 5-1. アプリが入ったか確認する

1. 本体のメニューに **MR LINK** が出ているか確認します。
2. MR LINK を開きます。

### 5-2. ログを見る（動作確認用）

ESP-IDF のシェルで次を実行すると、本体のログが見られます（`COM5` は自分の番号）。

```powershell
cd D:\RikuKojima\RaisingStackChan\firmware\.work
idf.py -p COM5 monitor
```

- MR LINK を開くと、ログに**トークン**が出ます（Quest との接続に使う合言葉です）。
- モニターを終了するには `Ctrl + ]` を押します。

### 5-3. WebSocket の受信を試す（上級者向け）

PC と StackChan が同じ Wi-Fi にいるとき、`websocat` というツールで次のように送ると、受信ログが出ます。

```
websocat "ws://<StackChanのIP>:8765/ws?token=<トークン>"
{"type":"emotion","value":"happy"}
```

> 現状の `MR LINK` は、WebSocket・mDNS・JSON の受信までです。QR 表示・表情の反映・首の固定は未実装です（`firmware/README.md` の「現状」）。

---

## 6. 困ったとき

| 症状 | 原因と対処 |
|---|---|
| `idf.py` が見つからない | ESP-IDF 用の PowerShell ではない。スタートメニューの「ESP-IDF 5.5 PowerShell」から開き直す |
| `upstream is empty. Run: git submodule update --init` | 手順 2-4 を実行していない。リポジトリ直下で `git submodule update --init` を実行する |
| `anchor not found in ...` | 公式ファームウェアの更新で、アプリ登録の目印の行が変わった。`firmware/build.ps1` の修正が必要。エラー文を共有する |
| `patches/xiaozhi-esp32.patch is not applied. Re-run with -Clean` | 依存ライブラリの取得が不完全。`.\build.ps1 -Clean` で最初からやり直す（再取得で時間がかかる） |
| ビルドが途中で止まる／おかしい | `.\build.ps1 -Clean` で `.work/` を消して最初から再実行する |
| `.work/sdkconfig` が古い設定のまま動く | `.work/sdkconfig` を削除するか、`-Clean` で作り直す（WebSocket の設定反映に必要） |
| COM ポートが出ない | データ通信対応ケーブルか確認／別の USB ポート／USB-Serial ドライバーをインストール／本体をダウンロードモードにする |
| 書き込み後に起動しない | もう一度書き込む。それでも駄目なら M5Stack 公式の手順で公式ファームウェアを書き戻す |
| 容量が足りない | `firmware/.work/` と ESP-IDF は大きい。不要ファイルを消して空きを作る |

---

## 7. 補足

### 公式ファームウェアを新しい版にしたいとき

`firmware/README.md` の「公式を更新するとき」を参照してください。コミットを指定して取り込む手順があります。

### 2 回目以降のビルド

`.work/` が残っていれば、依存ライブラリを取得し直さないので速くなります。

```powershell
cd D:\RikuKojima\RaisingStackChan\firmware
.\build.ps1                          # ビルド
.\build.ps1 -Task flash -Port COM5   # 書き込み
```

### コマンド早見表

| やりたいこと | コマンド（`firmware` フォルダで実行） |
|---|---|
| 初回の準備（公式ソース取得） | `git submodule update --init`（リポジトリ直下） |
| ビルド | `.\build.ps1` |
| 準備だけ | `.\build.ps1 -Task prepare` |
| 書き込み | `.\build.ps1 -Task flash -Port COM5` |
| 最初からやり直す | `.\build.ps1 -Clean` |
