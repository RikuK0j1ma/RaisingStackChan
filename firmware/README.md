# firmware

StackChan 公式ファームウェアに、自作アプリ `MR LINK` を足してビルドする。公式のファイルは編集しない。

```
firmware/
├ upstream/   公式 m5stack/StackChan（git submodule、コミット固定。編集禁止）
├ overlay/    自作ファイル。.work/ にそのままコピーされる（main/apps/app_mr_link/ など）
├ build.ps1   upstream を .work/ に複製 → overlay を重ねる → アプリ登録 → ビルド
└ .work/      作業コピー（gitignore）
```

## 仕組み

1. `upstream/firmware` を `.work/` に複製する。
2. `overlay/` を `.work/` に重ねる。公式の `main/CMakeLists.txt` は `apps/*.cpp` を自動で拾うので、`overlay/main/apps/app_mr_link/` を置くだけでビルド対象になる。
3. 公式ソースへの変更は、アプリ登録の2行だけ。`build.ps1` が改行コードを保ったまま挿入する（`apps.h` の include と `main.cpp` の `installApp`）。何度実行しても結果は同じ。
4. 依存ライブラリは公式の `fetch_repos.py` が `.work/` に取得する。

## 使い方

ESP-IDF 5.5.x のシェル（`idf.py` が使える状態）で実行する。

```powershell
git submodule update --init          # 初回のみ
.\build.ps1                          # ビルド
.\build.ps1 -Task prepare            # ビルドせず .work/ の準備だけ
.\build.ps1 -Task flash -Port COM5   # 実機へ書込み（工場出荷ファームが置き換わる）
.\build.ps1 -Clean                   # .work/ を消して最初から
```

## 公式を更新するとき

```powershell
git -C upstream fetch
git -C upstream checkout <新しいコミット>
git add upstream
```

登録2行のアンカー（`apps.h` / `main.cpp` の該当行）が変わるとビルドスクリプトが「anchor not found」で止まるので、そのときだけ `build.ps1` を直す。

## 現状

- `MR LINK` は雛形（QUIT ボタンだけ）。QR 表示と WebSocket サーバーは未実装。
- WebSocket サーバーの実装時に、`esp_http_server` などを `main/CMakeLists.txt` の `PRIV_REQUIRES` に足す必要がある。公式ファイルの書換えが要るので、そのときに `build.ps1` へ追加する。
- `build.ps1` は ESP-IDF の環境がある PC での動作を未確認。
