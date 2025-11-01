# SVG 過分割調査レポート（smoke-input.jpg）

- 作成日: 2025-11-01
- 担当: Codex

## 背景
`dotnet test` 実行時、スモークテスト成果物として生成される `layers-full-*.svg` において、均一色であるはずの領域が細かいレイヤーへ過分割される現象を確認した。

## 再現手順
1. `dotnet test` を実行（承認付き）。  
   - コマンド: `dotnet test`
2. 成果物ディレクトリ `tests/_artifacts/svg-smoke/<run-id>/` を確認。
3. `layer-*.svg` に多数の赤系レイヤーが生成され、`layers-full-*.svg` で円領域が細分化されていることを検証。

## 調査ログ
- Smoke テスト 115 件は全て成功。SixLabors.ImageSharp 3.1.4 に関する脆弱性警告が表示されるが、今回の症状とは無関係。
- 入力フィクスチャ `tests/fixtures/smoke-input.jpg`（112×112）は JPEG 圧縮ノイズを含み、RGB のユニークカラー数は 516 色。  
  - `python3` で集計したところ、最頻値は `(255, 255, 255)` が 7,760 ピクセル、赤系カラーも多数存在。
- 量子化処理 `Quantizer.QuantizeAsync`（`src/SvgCreator.Core/Quantizer.cs:33`）では既定設定 `QuantizerSettings.Default` を使用し、常に K-means（既定クラスタ数 20）を実行。  
  - ユニーク色数がクラスタ数より多いため、「ユニーク色数 ≦ k」の早期終了条件が満たされず、赤領域が複数クラスタへ分離。
- `ShapeLayerBuilder`（`src/SvgCreator.Core/ShapeLayers/ShapeLayerBuilder.cs:30`）のノイズ判定は `_options.NoisyComponentMinimumPixelCount` と `_options.NoisyComponentMinimumPerimeter` が既定 0 のため、微小成分も正規レイヤーとして保持される。
- `ImageReaderSettings.Default.EnableSmoothing` は `false` なので、量子化前にノイズを除去しない構成になっている。

## 推奨アクション案
1. 入力フィクスチャを PNG など非圧縮形式へ差し替える、または JPEG 品質を上げて圧縮ノイズを低減。
2. `QuantizerSettings.DefaultClusterCount` や `ShapeLayerBuilderOptions` のノイズしきい値を調整し、微小クラスタをノイズ扱いにする。
3. `ImageReaderSettings` で平滑化を有効化（ガウシアンぼかし）し、量子化前にノイズ成分を抑制。

## 参考: 論文での色数縮減とノイズ除去
- 論文「Image Vectorization with Depth」（`.sdd/specs/svg-creator/Paper_2409.06648v1.pdf`）では、K-means による色量子化で元画像の色数を大幅に削減する前提（p.4 付近、式 (9)）を採用している。
- 量子化後に残る微小な連結成分は 5.1 節「Denoising shape layers」で Definition 10／式 (15) に従ってノイズレイヤー `S_noise` に統合し、描画対象から除外する処理フローが示されている。
- 6.3 節では「Grouping quantization」を追加し、K-means の細分化結果と粗い意味的セグメンテーションを併用することで、色勾配や照明差による不要な分割を抑制する戦略を提案している。

## 今後の検討
- フィクスチャ差し替えに伴い `.sdd/specs/svg-creator/tasks.md` の該当手順、設計ドキュメントを更新する必要がある。
- 量子化パラメータ変更時には、既存テストへの影響やベンチマークを確認する。
