# 実装タスクリスト

## セクション1：データモデル実装
- [ ] 1.1 基本データモデルを実装する
  - design.md「主要コンポーネント」「データフロー」で定義された `ImageData`, `QuantizedImage`, `ShapeLayer`, `NoisyLayer`, `DepthOrder`, `BezierPath`, `ExportJob` などを C# のレコード/クラスとして整備する
  - PathSegment の点数制約や DepthIndex の非負といった不変条件をガード/ファクトリで担保し、最小限のユニットテストを追加する
- [ ] 1.2 デバッグ永続化レイヤーを実装する
  - design.md「品質管理/ログ」に基づき `--debug` 指定時の JSON スナップショット（`pic`, `palette`, `layers`, `depth`, `paths`）を System.Text.Json で保存する
  - バージョン識別子と簡易ローダーを用意し、requirements.md のデバッグ要件と互換性を確認する

## セクション2：ビジネスロジック実装
- [ ] 2.1 Orchestrator パイプライン制御を実装する
  - design.md「コンポーネント1 Orchestrator」の処理順/品質プリセット/スレッド制御を組み込み、進捗イベントを発火させる
  - Microsoft.Extensions.Logging による標準ログとキャンセル対応（Ctrl+C）を整備する
- [ ] 2.2 ImageReader / Preprocessor を実装する
  - design.md「コンポーネント2」に従い OpenCvSharp で画像読み込み・CIELAB 変換・必要最低限のノイズ軽減を行う
  - エラー発生時の復帰戦略とメタデータ抽出をまとめ、要件6のガイド文言を準備する
- [ ] 2.3 Quantizer（K-means）を実装する
  - design.md「コンポーネント3」の仕様に沿い ML.NET KMeans（k-means++ 初期化）で量子化し、パレットと初期レイヤーマップを生成する
  - `--quality` プリセットに応じて K/反復回数を切り替え、並列化オプションを反映する
- [ ] 2.4 ShapeLayerBuilder を実装する
  - design.md「コンポーネント5」のフローで連結成分抽出→輪郭トレース→微小領域除去→ノイズ層分離を行い、`ShapeLayer[]`/`NoisyLayer[]` を返す
  - 生成結果が自己交差や穴を持たないか確認する簡易バリデーションを実装する
- [ ] 2.5 DepthOrdering を実装する
  - design.md「コンポーネント6」の手順で隣接境界長評価→スコア付け→SCC 縮約→重み付きトポロジカルソートを構築する
  - 閉路解消不能ケースのフォールバック（同深度許容＋警告）と要件6の通知を整備する
- [ ] 2.6 OcclusionCompleter を実装する
  - design.md「コンポーネント7」に沿ってエラスティカ最適化（曲率二乗＋弧長、Math.NET Numerics＋自前 L-BFGS）を構築する
  - 収束失敗時のリトライや直線補間へのフォールバックを実装し、ログ/警告を出力する
- [ ] 2.7 BezierFitter を実装する
  - design.md「コンポーネント8」の三次 Bézier 近似と Douglas–Peucker ベースの単純化を実装し、最大法線距離閾値を品質プリセットと連動させる
  - 曲率ペナルティを考慮した節点再配置ロジックを追加する
- [ ] 2.8 SvgEmitter / ExportFilter を実装する
  - design.md「コンポーネント9」「ワークフロー9」に基づき `<svg>` / `<g id=\"layer-###\" data-depth>` / `<path>` を System.Xml.Linq で生成する
  - `--export-layer` と 15KB 制約（SizeLimiter）の再単純化ロジックで requirements.md 要件4・5を満たす

## セクション3：インターフェース実装
- [ ] 3.1 CLI コマンドとオプションを整備する
  - design.md「Orchestrator」および requirements.md 要件7 のオプション (`--image`, `--out`, `--threads`, `--quality`, `--export-layer`, `--debug`, `--quiet`, `--verbose`) を System.CommandLine で定義する
  - `--help` 出力と使用例を記述し、英語/日本語メッセージを準備する
- [ ] 3.2 入力バリデーションを実装する
  - requirements.md 要件1/6/7に従い、画像パス存在チェック・非対応形式検知・数値パラメータ範囲（K、delta、eulerMaxIter など）の検証を行う
  - 不正入力時に適切な終了コード、再提案メッセージ、ヘルプ誘導を返す
- [ ] 3.3 出力フォーマットとファイル命名を整備する
  - 単一 SVG とレイヤ別 SVG の出力先 (`out/` ディレクトリ) を整理し、`layer-003.svg` などの命名規約を実装する
  - 生成 SVG に `data-depth` メタデータと viewBox/サイズ情報を付与し、一般的な SVG ビューアで表示確認を行う

## セクション4：統合とテスト
- [ ] 4.1 パイプライン統合と進捗表示を検証する
  - design.md「ワークフロー」を Orchestrator に接続し、前処理→減色→抽出→整序→補完→出力の進捗ログが要件6を満たすことを確認する
  - `--quiet` / `--verbose` の切替えと処理キャンセル時の終了コード/メッセージを確認する
- [ ] 4.2 単体・結合テストを整備する
  - xUnit 等で Quantizer, DepthOrdering, BezierFitter, SizeLimiter の主要メソッドをカバレッジ対象とする
  - 小規模テスト画像を使った結合テストでレイヤ境界ギャップなし・閉路ゼロ・15KB 以下・部分出力成功を検証する
- [ ] 4.3 受入基準チェックリストを更新する
  - requirements.md 要件1〜7 と非機能要件（並列化、ライセンス、XML ドキュメント出力）への適合状況を記録する
  - LICENSE/NOTICE 配置とドキュメントコメント警告（CS1591）未発生を確認し、必要に応じて README.md を更新する
