# 実装タスクリスト

## セクション1：データモデル実装
- [x] 1.1 必要な型定義・データ構造を作成する
  - design.mdで定義された `ImageData`, `ShapeLayer`, `DepthOrder`, `Path`, `PathSegment`, `QuantizationResult` などのモデルを実装する
  - 凸性・非交差といったバリデーションをガードまたは契約として実装する
- [x] 1.2 デバッグ永続化層を実装する
  - `--debug` オプション用の JSON 入出力（中間画像・レイヤー・パレット情報）を整備する
  - バージョン情報と整合性チェックを追加する

## セクション2：ビジネスロジック実装
- [ ] 2.1 Orchestrator（CLIコア）の処理を実装する
  - design.mdの処理フロー ステップ1-3（CLI引数解析〜進捗表示）に対応する
- [ ] 2.2 ImageReader / Preprocessor を実装する
  - design.mdの処理フロー ステップ2-3（入力読込・色空間変換・前処理）に対応する
- [ ] 2.3 Quantizer（K-means減色）を実装する
  - design.mdの処理フロー ステップ4（量子化）に対応し、ML.NETベースのK-meansを構成する
- [ ] 2.4 ShapeLayerBuilder（領域抽出）を実装する
  - design.mdの処理フロー ステップ5（連結成分抽出・輪郭生成）に対応する
- [ ] 2.5 DepthOrdering（深度整序）を実装する
  - design.mdの処理フロー ステップ6（隣接グラフ算出・SCC縮約・トポロジカルソート）に対応する
- [ ] 2.6 OcclusionCompleter（エラスティカ補完）を実装する
  - design.mdの処理フロー ステップ7（遮蔽部補完）に対応し、Math.NET＋自前L-BFGSで最適化する
- [ ] 2.7 BezierFitter（曲線近似）を実装する
  - design.mdの処理フロー ステップ8（Bézierフィット・単純化）に対応する
- [ ] 2.8 SvgEmitter（SVG出力）を実装する
  - design.mdの処理フロー ステップ9（`<svg>/<g>/<path>` 生成と属性最適化）に対応する
- [ ] 2.9 ExportFilter / SizeLimiter を実装する
  - design.mdの処理フロー ステップ9（レイヤ別出力と15KB制約対応）に対応する
- [ ] 2.10 エラーハンドリングを実装する
  - design.mdに列挙されたエラーケース（入力不正・未収束・サイズ超過等）の処理を網羅する

## セクション3：インターフェース実装
- [ ] 3.1 CLIコンポーネントを作成する
  - `svg-creator` メインコマンドとサブコマンド/オプションを System.CommandLine で定義する
- [ ] 3.2 入力バリデーションを実装する
  - ファイルパス存在確認や `--quality`, `--threads`, `--export-layer` などの値域チェックを実装する
- [ ] 3.3 出力フォーマットを実装する
  - 単一SVGとレイヤ別SVGのファイル命名・メタデータ（`layer-XXX.svg`, `data-depth` 属性）を整える

## セクション4：統合とテスト
- [ ] 4.1 コンポーネントを統合する
  - Orchestrator に全コンポーネントを配線し、データフローとスレッド制御を確認する
- [ ] 4.2 基本的な動作テストを実装する
  - 単体テスト（Quantizer, DepthOrdering, BezierFitter, SizeLimiter 等）と統合テスト（小規模入力で要求満足）を作成する
- [ ] 4.3 要件の受入基準を満たすことを確認する
  - requirements.md の要件（凸性チェック、レイヤ別出力、進捗表示、CLI挙動）に対する受入確認を記録する
