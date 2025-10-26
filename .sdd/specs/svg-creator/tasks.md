# 実装タスクリスト
[調査結果](C:\Users\taiga\source\repos\SvgCreator\.sdd\specs\svg-creator\research-python-code-structure.md)と、設計を元に実装を行うこと

## セクション1：データモデル実装
- [x] 1.1 必要な型定義・データ構造を作成する
  - design.mdで定義された `ImageData`, `ShapeLayer`, `DepthOrder`, `Path`, `PathSegment`, `QuantizationResult` などのモデルを実装する
  - 凸性・非交差といったバリデーションをガードまたは契約として実装する
- [x] 1.2 デバッグスナップショットモデルを実装する
  - `DebugSnapshot` ファミリー（Image/Layer/Mask、`CliOptions`、`Version`）と JSON シリアライザを整備する
  - バージョン互換チェックの実装と単体テストを追加する
- [x] 1.3 デバッグ出力レイアウト仕様を固める
  - `metadata.json` で用いるファイルメタ情報のスキーマを確定する
  - `pipeline.json`, `layers.json`, 追加アセット（画像ダンプ等）の命名・配置規則を設計し、ヘルパーで表現できるようにする

## セクション2：ビジネスロジック実装
- [x] 2.1 Orchestrator（CLIコア）の処理を実装する
  - design.mdの処理フロー ステップ1-3（CLI引数解析〜進捗表示）に対応する
  - `IDebugSink` を組み込み、ステージ完了時にスナップショットを送出できるようにする
- [x] 2.2 ImageReader / Preprocessor を実装する
  - design.mdの処理フロー ステップ2-3（入力読込・色空間変換・前処理）に対応する
  - Windows 11 と Ubuntu 22.04 の両方で動作するよう `OpenCvSharp4.runtime.any` などのランタイムパッケージ選定と依存登録を行い、OS依存箇所を排除する
- [x] 2.3 Quantizer（K-means減色）を実装する
  - design.mdの処理フロー ステップ4（量子化）に対応し、ML.NETベースのK-meansを構成する
- [ ] 2.4 OpenCvSharp のクロスプラットフォーム対応を整備する
  - Windows 固有の API やパス依存を排除し、Linux でも同一の処理が動作するよう ImageReader/Preprocessor の実装をリファクタリングする
  - `OpenCvSharp4.runtime.any` もしくは OS 別ランタイムの導入手順をコードコメント/設定に反映し、ビルド定義を更新する
- [ ] 2.5 ShapeLayerBuilder（領域抽出）を実装する
  - design.mdの処理フロー ステップ5（連結成分抽出・輪郭生成）に対応する
- [ ] 2.6 DepthOrdering（深度整序）を実装する
  - design.mdの処理フロー ステップ6（隣接グラフ算出・SCC縮約・トポロジカルソート）に対応する
- [ ] 2.7 OcclusionCompleter（エラスティカ補完）を実装する
  - design.mdの処理フロー ステップ7（遮蔽部補完）に対応し、Math.NET＋自前L-BFGSで最適化する
- [ ] 2.8 BezierFitter（曲線近似）を実装する
  - design.mdの処理フロー ステップ8（Bézierフィット・単純化）に対応する
- [ ] 2.9 SvgEmitter（SVG出力）を実装する
  - design.mdの処理フロー ステップ9（`<svg>/<g>/<path>` 生成と属性最適化）に対応する
- [ ] 2.10 ExportFilter / SizeLimiter を実装する
  - design.mdの処理フロー ステップ9（レイヤ別出力と15KB制約対応）に対応する
- [ ] 2.11 デバッグ出力シンク（File/Null）を実装する
  - `out/debug/` レイアウトとファイル命名を 1.3 の仕様に従って実装する
  - `--debug-dir`, `--debug-stages`, `--debug-keep-temp` に応じたフィルタリングやクリーンアップを実現する
- [ ] 2.12 エラーハンドリングを実装する
  - design.mdに列挙されたエラーケース（入力不正・未収束・サイズ超過等）の処理を網羅する
  - デバッグ出力失敗・バージョン不一致時の例外処理とログ通知を追加する

## セクション3：インターフェース実装
- [ ] 3.1 CLIコンポーネントを作成する
  - `svg-creator` メインコマンドとサブコマンド/オプションを System.CommandLine で定義する
- [ ] 3.2 入力バリデーションを実装する
  - ファイルパス存在確認や `--quality`, `--threads`, `--export-layer` などの値域チェックを実装する
- [ ] 3.3 出力フォーマットを実装する
  - 単一SVGとレイヤ別SVGのファイル命名・メタデータ（`layer-XXX.svg`, `data-depth` 属性）を整える
- [ ] 3.4 デバッグ関連オプションを実装する
  - `--debug`, `--debug-dir`, `--debug-stages`, `--debug-keep-temp` を定義し、値検証と Orchestrator へのバインドを行う

## セクション4：統合とテスト
- [ ] 4.1 コンポーネントを統合する
  - Orchestrator に全コンポーネントを配線し、データフローとスレッド制御を確認する
- [ ] 4.2 クロスプラットフォーム依存性を検証する
  - Windows 11 / Ubuntu 22.04 想定環境で OpenCvSharp を含むネイティブ依存が解決されることを確認し、ファイルパスや一時ディレクトリの処理が OS 非依存であることをテストする
- [ ] 4.3 基本的な動作テストを実装する
  - 単体テスト（Quantizer, DepthOrdering, BezierFitter, SizeLimiter 等）と統合テスト（小規模入力で要求満足）を作成する
- [ ] 4.4 デバッグ出力の統合テストを実装する
  - 一時ディレクトリに `pipeline.json`, `layers.json`, `metadata.json` が生成され、要件8（バージョン・CLIオプション写し・ステージ絞り込み）が満たされることを検証する
- [ ] 4.5 要件の受入基準を満たすことを確認する
  - requirements.md の要件（凸性チェック、レイヤ別出力、進捗表示、CLI挙動、デバッグ出力）に対する受入確認を記録する
- [ ] 4.6 README でネイティブ依存の導入手順を記載する
  - ランタイムパッケージの選択理由と導入手順を README に追記し、プラットフォーム固有の設定が不要である旨を明示する
