# CLAUDE.md

このリポジトリで作業する Claude 向けの申し送り。構成は PhysicalAITrainer（`D:\RikuKojima\PhysicalAITrainer`）に合わせてある。

---

## これは何か・今どこまで進んでいるか

Quest の MR で、StackChan の顔の位置に球体（餌）を出し、口に近づけると消えて StackChan が喜び顔になるアプリ。StackChan は M5Stack 公式ファームに自作アプリ `MR LINK` を足して使い、Quest（Unity）とは Wi-Fi の WebSocket で通信する。

- 設計とユーザー体験は `docs/design.md`。作業を始める前にここと該当 Issue だけ読めば足りる。
- 残作業は GitHub の Issue #1〜#15（`gh issue list`）。依存関係は各 Issue の「依存」欄にある。
- 現状は骨組みだけ（PR #16）。Unity のコードも、`MR LINK` の QR・WebSocket も未実装。Unity は一度もエディタで開いていない。`idf.py build` も未実行。

---

## トークンを節約するために

- 公式ファーム（`firmware/upstream`）を探索しない。知りたい場所は下記のとおり。それ以外は `Grep` で目的の語だけ探す。
  - アプリの雛形: `main/apps/app_template/`
  - アプリ登録: `main/main.cpp`（`installApp`）、`main/apps/apps.h`
  - 表情: `main/stackchan/avatar/avatar/elements/emotion.h`（`Emotion::Happy` ほか）
  - QR 表示の使用例: `main/apps/app_setup/workers/connectivity.cpp`（`Qrcode`）
  - 首の自動動作: `main/stackchan/modifiers/idle_motion.h`
  - `apps/*.cpp` は `main/CMakeLists.txt` が自動で拾う（追加アプリはファイルを置くだけ）
- 次のファイルは長いので全文を読まない: `main/CMakeLists.txt`（約400行、上の2点が分かっていれば不要）、`main/hal/hal_ws_avatar.cpp`、`main/hal/utils/local_network/local_network_protocol.h`。
- 公式の LAN WebSocket（`hal/utils/local_network`、StackChan がクライアントでクラウド経由）と xiaozhi 関連は今回使わない。読まない。
- `firmware/.work/` と `firmware/upstream/` は検索対象から外す（`Grep` / `Glob` の `path` を限定する）。`.work` には依存の clone が入っていて、結果が膨れる。
- `build.ps1` は初回に依存を clone するので数分かかり、容量も大きい。`.work` が残っていれば 2 回目以降は速い。目的が登録の確認だけなら `-Task prepare`。
- Unity の構成の参考は `D:\RikuKojima\PhysicalAITrainer`（特に `CLAUDE.md` と asmdef）。ファームの参考は `D:\RikuKojima\StackChan`（公式のローカル clone）。どちらも全体を読まず、必要な 1 ファイルだけ開く。

---

## つまずきやすい点

- **改行コード**: Windows の git は clone を CRLF にする。公式ファイルは CRLF で、差分パッチ（`.patch`）や文字列一致の編集が壊れやすい。ファイルへの挿入は改行を保つこと（`build.ps1` の `Add-LineAfter` を参照）。
- **カレントディレクトリ**: `cd` でサブモジュール内（`firmware/upstream` など）に入ると、以後の作業ディレクトリがそこに残り、誤ってサブモジュール側へ書き込む。絶対パスを使い、`cd` は避ける。
- **`.ps1` は ASCII だけ**: Write ツールは BOM なしの UTF-8 で書く。Windows PowerShell 5.1 は日本語を文字化けさせるので、メッセージとコメントは英語にする。
- **PowerShell 5.1**: `&&` / `||` が使えない。`$ErrorActionPreference='Stop'` のとき、ネイティブコマンドの stderr で止まることがある（`2>$null` を付ける箇所は一時的に `Continue` にする）。
- **Bash でのクォート**: 日本語・バッククォート・引用符を含む長い本文を `gh issue create` などへ渡すとき、`"..."` の連結は壊れやすい。本文はファイルに書き、`--body-file` か、Python スクリプトで渡す。
- **Git LFS**: `.gitattributes` で `.meta` を含む多くの形式が LFS 対象。clone と push には Git LFS が必要。
- **サブモジュール**: 初回は `git submodule update --init`。固定コミットを動かすときは、`firmware/README.md` の手順に従い、`git add firmware/upstream` まで行う。
- **Meta SDK のアセンブリ名**: `Platform.Meta` の asmdef は、Meta 固有の参照をまだ持たない。実装時に、導入した SDK で名前を確認してから足す（推測で書かない）。
- **R3** は入っていない。使うなら NuGetForUnity で入れてから、asmdef に `R3.Unity` を足す。

---

## 確認が要る操作

- 実機への書込み（`build.ps1 -Task flash`）: 工場出荷ファームを置き換える。
- `firmware/upstream` の編集: 公式リポジトリのものなので禁止。

---

## 書き方

- ドキュメントと Issue・PR は日本語。C++ のコメントは公式に合わせて英語。
- コミットは英語の短い件名＋本文。PR の本文は日本語。

---

## リポジトリ構成

```
unity/      Quest 用 MR アプリ
firmware/   StackChan ファームウェア
docs/       設計
```

---

## 公式ファームウェアは編集しない

- `firmware/upstream` は M5Stack の公式リポジトリ（submodule）。このリポジトリのものではないので、中のファイルは編集しない。コミット固定の更新は `git -C firmware/upstream checkout <commit>` と `git add firmware/upstream` で行う。
- 自作のコードは `firmware/overlay/` に置く。`firmware/build.ps1` が `upstream` を `firmware/.work/` に複製し、`overlay` を重ねてビルドする。
- 公式ソースに入れる変更は、`build.ps1` の「アプリ登録」の行挿入だけ。差分パッチ（`.patch`）は使わない。公式のファイルは CRLF で、環境の改行設定によって当たらなくなるため。
- 実機への書込み（`build.ps1 -Task flash`）は工場出荷ファームを置き換える。ユーザーに確認してから行う。

---

## 名前（Unity）

- 名前空間と asmdef 名は `RikuK0j1ma.RaisingStackChan.<層>`。両者は一致させる。
- 製品固有のアセットは `unity/Assets/__RikuK0j1ma__/__RaisingStackChan__/` 以下に置く。個人の試作は `unity/Assets/__RikuK0j1ma__/Sample/<名前>/`。
- 「Imacreate」を含む名前・パッケージは使わない。

---

## 層とアセンブリ定義（Unity）

スクリプトは `Script/` 以下。依存の向きは次のとおりで、逆向きの参照は追加しない。

```
Utility ← Extension ← Entity ← UseCase ← Infrastructure / Presentation / Controller / Platform.Meta
                                                       ↑
                                                    Context
```

| 層 | 置くもの |
|---|---|
| Entity | 設定値（ScriptableObject）、値型、列挙 |
| UseCase | アプリの手順と、外部機能のインターフェース（`IStackChanLink`、`IStackChanPoseProvider` など）。UI・Unity の描画系・VContainer には依存しない |
| Infrastructure | UseCase のインターフェースの Unity 実装（WebSocket、アンカー永続化）と、実機が無いときの Null 実装 |
| Platform.Meta | Quest 固有の実装（QR 検出、Passthrough、Spatial Anchor）。Editor と Android だけ |
| Presentation | 画面表示（球体、ガイド UI） |
| Controller | 入力（ハンド、コントローラー） |
| Context | VContainer の LifetimeScope とエントリーポイント |
| DebugTools | 開発ビルド専用。`RSC_DEVELOPMENT` が無いとアセンブリごとビルドから外れる。Context からは参照しない |
| Editor | エディタ拡張 |

- 共通の層の asmdef は対象プラットフォームを指定しない。
- 製品の asmdef からテスト用アセンブリ（TestRunner）は参照しない。

### 定義シンボル

- `RSC_DEVELOPMENT`: 開発ビルド。
- Meta 固有の asmdef の参照（`Oculus.VR`、MRUK など）は、実装するときに足す。名前は導入した SDK で確認する。

### 依存の追加

- R3 は NuGetForUnity で `Assets/Packages/` に入れる。入れたら、使う層の asmdef に `R3.Unity` を足す。
