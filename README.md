# RaisingStackChan

StackChan と Meta Quest の MR アプリの連携。

Quest のパススルー映像の中で、StackChan のいる場所に球体（餌）を出す。球体が StackChan の口に十分近づくと球体が消え、StackChan が喜び顔になる。

## 構成

```
unity/       Quest 用 MR アプリ（Unity）
firmware/    StackChan ファームウェア（公式 + 自作アプリ MR LINK）
docs/        設計
```

- 設計とユーザー体験: [docs/design.md](docs/design.md)
- ファームウェアのビルド: [firmware/README.md](firmware/README.md)
- Unity の層構造と規約: [CLAUDE.md](CLAUDE.md)

## Unity

- バージョン: 6000.4.1f1
- 開くプロジェクト: `unity`
- レンダリング: Universal Render Pipeline
- XR: OpenXR（Android / Quest 3・3S）

### Dependencies
| Name | Version |
|---|---|
| Meta XR All-in-One SDK | 207.0.1 |
| Universal RP | 17.4.0 |
| Input System | 1.19.0 |
| OpenXR Plugin | 1.16.1 |
| XR Plugin Management | 4.5.2 |
| VContainer | 1.19.0 |
| UniTask | 2.5.11 |
| MessagePipe (+ VContainer) | 1.8.2 |
| ZString | 2.6.0 |
| LitMotion | 2.0.2 |
| NuGetForUnity | 4.5.0 |
| MCP for Unity | 10.3.0 |
| Newtonsoft Json | 3.2.2 |
| Android Logcat | 1.4.7 |

R3 は NuGetForUnity 経由で入れる。最初の実装で必要になった時点で追加する。

## StackChan

公式ファームウェア（`firmware/upstream`、M5Stack の StackChan リポジトリを submodule で固定）に、自作アプリを重ねてビルドする。

```powershell
git clone --recurse-submodules <このリポジトリ>
```
