# CLAUDE.md

このリポジトリで作業する Claude 向けの申し送り。構成は PhysicalAITrainer（`D:\RikuKojima\PhysicalAITrainer`）に合わせてある。

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
