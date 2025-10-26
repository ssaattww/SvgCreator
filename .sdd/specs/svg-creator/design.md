# 技術設計書

## アーキテクチャ概要
- 目的: 入力ラスタ画像から「前後関係（深度順）」を伴うレイヤ分解SVGを生成するCLIツール（SvgCreator）。
- 方式: 画像の減色→領域抽出→隣接グラフと深度整序→遮蔽部の曲率正則化補完（エラスティカ近似）→Bézier近似→SVG出力。
- 技術統合方針（steering/tech.md 前提）:
  - .NETベースのCLIとして実装（例: .NET 8, C#）。
  - 数値・画像・SVGは下記候補を採用（research-libraries-mapping.md 参照）:
    - 画像/形態: OpenCvSharp（OpenCV）、描画系: SkiaSharp（任意）。Windows と Ubuntu の双方で動作するように `OpenCvSharp4.runtime.any` または Linux/Windows のランタイムパッケージを併用する。
    - 数値/最適化: Math.NET Numerics（必要に応じて ALGLIB）
    - グラフ: QuikGraph
    - SVG入出力: SVG.NET（必要に応じて SharpVectors / Svg.Skia）
  - プラットフォーム要件（Windows 11 / Ubuntu 22.04）を満たすため、ファイルパスや依存定義をOS非依存に保ち、追加のネイティブ依存が必要な場合は README に導入手順を明記する。
  - 既存要件（requirements.md）とCLI仕様を一致させ、レイヤ別15KB以内、部分出力（指定レイヤのみ）をサポート。

関連資料
- 方式の根拠と論文の節番号: research-depth-vectorization.md
- Python実装の調査: research-python-impl.md, research-python-code-structure.md
- C#ライブラリ候補: research-libraries-mapping.md

## 主要コンポーネント
### コンポーネント1: Orchestrator（CLI）
- 責務: パラメータ受け取り、各ステージの実行順制御、進捗表示、ログ出力、部分出力制御、デバッグスナップショットのトリガー。
- 入力: CLI引数（`--image`, `--out`, `--quality`, `--K`, `--delta`, `--euler-max-iter`, `--a`, `--b`, `--eps`, `--b-eps`, `--b-radius`, `--export-layer`, `--threads`, `--quiet/--verbose`, `--debug`, `--debug-dir`, `--debug-stages`, `--debug-keep-temp` など）。
- 出力: 最終SVG, 中間成果（任意で保存）。
- 依存関係: System.CommandLine（CLI定義）。
- 検討した選択肢（不採用）: Spectre.Console.Cli, CommandLineParser。

### コンポーネント2: ImageReader / Preprocessor
- 責務: 入力画像の読込、色空間変換、任意のノイズ除去/減色前のスムージング。
- 入力: 画像ファイル（PNG/JPEG）。
- 出力: `ImageData`（幅/高さ/画素配列）。
- 依存関係: OpenCvSharp。
- 検討した選択肢（不採用）: SkiaSharp（機能不足箇所は自前実装が増える）。

### コンポーネント3: Quantizer（減色）
- 責務: K-means等で色量子化して領域の一貫性を高める。
- 入力: `ImageData`, パラメータ `K`。
- 出力: 量子化後画像、色パレット、初期レイヤ（画素ラベル）。
- 依存関係: ML.NET（KMeans）。
- 検討した選択肢（不採用）: 自前実装, Accord.NET。

### コンポーネント4: Segmenter（任意・多相）
- 責務: 実画像向けに量子化後の粗いグルーピング（多相セグメンテーション）。
- 入力: 量子化結果、`mu`, `maxPhases`, `segMaxIter`。
- 出力: セグメンテーション結果（フェーズ）。
- 依存関係: Math.NET Numerics（数値最適化）。

### コンポーネント5: ShapeLayerBuilder
- 責務: 連結成分抽出→領域（境界ポリライン/輪郭）を生成、微小領域除去、ノイズ層分離。
- 入力: 量子化/セグメンテーション結果、`noisyThreshold`。
- 出力: `ShapeLayer[]`, `NoisyLayer[]`。
- 依存関係: OpenCvSharp。
- 検討した選択肢（不採用）: SkiaSharp（形態演算/輪郭で不足）。

### コンポーネント6: DepthOrdering
- 責務: 隣接レイヤ間の「手前/奥」スコアを算出し、閉路を抑制しつつ全体の深度順を決定。
- 入力: `ShapeLayer[]`, パラメータ `delta`。
- 出力: `DepthOrder`（`layerId -> depthIndex`）。
- 依存関係: 自前DAGユーティリティ（SCC/トポロジカルソート）。
- 検討した選択肢（不採用）: QuikGraph（MS-PL・依存削減方針）。

### コンポーネント7: OcclusionCompleter（Elastica近似）
- 責務: 遮蔽で欠落した輪郭を曲率正則化＋弧長ペナルティで補完（離散エラスティカの近似最適化）。
- 入力: `ShapeLayer[]`, `DepthOrder`, パラメータ `eulerMaxIter`, `eps`, `a`, `b`, `r`。
- 出力: 補完済み境界（閉域）。
- 依存関係: Math.NET Numerics。
- 検討した選択肢（不採用）: ALGLIB（非MIT/依存最小化）。

### コンポーネント8: BezierFitter
- 責務: 境界をBézier（2次/3次）へ近似し、曲率変化に応じた節点密度で単純化。
- 入力: 補完済み境界、`bEps`, `bRadius`。
- 出力: `Path[]`（SVG Path セグメント列）。
- 依存関係: System.Numerics（SIMD）。
- 検討した選択肢（不採用）: NetTopologySuite（現状要件では過剰）。

### コンポーネント9: SvgEmitter
- 責務: 深度順に `<g id="layer-XXX" data-depth="d">` としてレイヤを並べ、スタイル/メタデータを付与してSVG化。
- 入力: `Path[]`, `DepthOrder`, 画像メタ（幅/高さ/viewBox）。
- 出力: SVGファイル（単一/レイヤ別）。
- 依存関係: System.Xml.Linq（自前Writer）。
- 検討した選択肢（不採用）: SVG.NET, SharpVectors（MS-PL/DOM過剰）, Svg.Skia（描画検証は任意プラグイン）。

### コンポーネント10: ExportFilter / SizeLimiter
- 責務: `--export-layer` に従った部分出力、各レイヤ≤15KB制約の検査と再単純化。
- 入力: `Path[]`, 対象レイヤID, サイズ制約。
- 出力: レイヤ別SVG。
- 依存関係: SvgEmitter, BezierFitter。

### コンポーネント11: DebugSink（File / Null）
- 責務: `--debug` 有効時にステージごとの `DebugSnapshot` を生成し、ファイルへ書き出す。無効時は処理をスキップする。
- 入力: `DebugSnapshot`, ステージ識別子、CLI オプション（保存先、対象ステージなど）。
- 出力: JSONファイル（`pipeline.json`, `layers.json`, `metadata.json` 等）、画像ダンプ（任意）。
- 依存関係: System.Text.Json, System.IO, Logging（保存結果の通知）。

## データモデル
### ImageData
- `Width`: int – 画像幅
- `Height`: int – 画像高
- `Pixels`: byte[] / Span<byte> – RGB(A)列

### ShapeLayer
- `Id`: string – レイヤID
- `Color`: (byte r, byte g, byte b)
- `Mask`: BitArray/RunLength – 領域マスク
- `Boundary`: List<PointF> – 外周輪郭（CCW）
- `Holes`: List<List<PointF>> – 穴領域
- `Area`: int – 画素数

### AdjacencyGraph
- `Nodes`: List<ShapeLayer>
- `Edges`: List<AdjacencyEdge>

### AdjacencyEdge
- `A`: string – layerId
- `B`: string – layerId
- `SharedLen`: float – 共有境界長
- `ScoreAB`: float – 「AがBより手前」スコア（0..1）

### DepthOrder
- `DepthByLayer`: Dictionary<string,int> – 0=最奥

### Path/PathSegment
- `Segments`: List<PathSegment>
- `PathSegment`: { `Type`: enum(M,L,C,Q,Z), `Points`: PointF[] }

### DebugSnapshot
- `Version`: string – スナップショットフォーマットのバージョン（例: "1.0"）
- `Image`: DebugSnapshotImage – 入力画像のメタ情報とピクセルデータ
- `Palette`: List<RgbColor> – 量子化で得たパレット
- `Layers`: List<DebugSnapshotLayer> – 生成済みレイヤーの詳細
- `CreatedAt`: DateTimeOffset – 書き出し時刻（Serializerが付与）
- `CliOptions`: Dictionary<string, object> – 実行時の主要 CLI オプション写し

### DebugSnapshotImage / Layer / Mask
- DebugSnapshotImage: `Width`, `Height`, `Format`, `Pixels`（byte[]）
- DebugSnapshotLayer: `Id`, `Color`, `Area`, `Mask`, `Boundary`, `Holes`
- DebugSnapshotMask: `Width`, `Height`, `Bits`（bool[]）

## 処理フロー
1. 画像読み込み（ImageReader）→ `ImageData` を得る
2. 減色（Quantizer, K既定=20）→ 量子化画像・色パレット・初期レイヤ
3. 任意セグメンテーション（Segmenter, 実画像向け）
4. 領域抽出（ShapeLayerBuilder）→ `ShapeLayer[]`（微小領域除去）
5. 隣接グラフ構築と深度整序（DepthOrdering, `delta` 既定=0.05, SCC縮約→トポソート）
6. 遮蔽補完（OcclusionCompleter, `eulerMaxIter` 既定=100, `eps`=5, `a`=0.1, `b`=1.0, `r`=0.1）
7. Bézier近似・単純化（BezierFitter, `bEps`=1.0, `bRadius`=0.8 目安）
8. SVG出力（SvgEmitter, viewBox設定, `<g>`に`data-depth`付与）
9. 部分出力/サイズ制約（ExportFilter/SizeLimiter, レイヤ≤15KBを満たさない場合は再単純化）
10. デバッグスナップショット（DebugSink, `--debug` 有効時のみ）
    - ステージ名と `DebugSnapshot` を受け取り、`out/debug/` または `--debug-dir` で指定されたディレクトリへ JSON ファイルを保存
    - `--debug-stages` 指定時は対象ステージのみ書き出し、`metadata.json` にファイル一覧とバージョン情報を更新
    - `--debug-keep-temp=false` の場合、成功後に不要な中間ファイルを削除

品質プリセット例（`--quality {fast|balanced|high}`）
- fast: K=12, eulerMaxIter=40, bEps=2.0
- balanced: K=20, eulerMaxIter=100, bEps=1.0
- high: K=28, eulerMaxIter=200, bEps=0.7

## エラーハンドリング
- 入力ファイル不正/未対応形式 → 明示メッセージ＋ヘルプ表示
- K が小さすぎ/大きすぎ → 既定範囲に丸め（例: 4..64）
- 連結成分なし/極端なノイズ → ガイド付き設定（Kや`noisyThreshold`再提案）
- 深度整序で閉路解消不可 → 共有境界長優先で丸め、未解消は警告付きで同深度並置
- エラスティカ未収束 → 反復上限で打切り、弧長重み増で再試行→直線補間フォールバック
- レイヤ>15KB → 追加単純化/カーブ分割、なお超過時は警告付き許容範囲を提示
- 出力書込例外（パス/権限） → 代替パス提案、終了コード≠0
- デバッグスナップショット書込失敗 → エラー内容をログ出力し、CLI 終了コード≠0（`--debug` は検証用途のため失敗時は中断）
- スナップショットバージョン不一致 → `InvalidDataException` を発生させ CLI 側で警告表示、`metadata.json` に互換性情報を記録

## 既存コードとの統合
- 変更が必要なファイル：
  - `src/SvgCreator.CLI/Program.cs`: CLI定義・ヘルプ・プリセット（新規作成時は該当なし）
  - `src/SvgCreator.CLI/Options/DebugOptions.cs`（仮）: `--debug` 系オプションのバインディング
  - `src/SvgCreator.Core/*`: 各コンポーネント実装（新規）
  - `.sdd/specs/svg-creator/requirements.md`: オプション説明の整合（必要なら追補）
- 新規作成ファイル：
  - `src/SvgCreator.Core/ImageReader.cs`: 画像I/O
  - `src/SvgCreator.Core/Quantizer.cs`: KMeans減色
  - `src/SvgCreator.Core/Segmenter.cs`: 多相セグメンテーション（任意）
  - `src/SvgCreator.Core/ShapeLayerBuilder.cs`: 領域抽出
  - `src/SvgCreator.Core/DepthOrdering.cs`: 隣接グラフと整序
  - `src/SvgCreator.Core/OcclusionCompleter.cs`: エラスティカ近似
  - `src/SvgCreator.Core/BezierFitter.cs`: 曲線近似/単純化
  - `src/SvgCreator.Core/SvgEmitter.cs`: SVG生成
 - `src/SvgCreator.Core/ExportFilter.cs`: 部分出力/サイズ制御
  - `src/SvgCreator.Core/Models/*.cs`: データモデル
  - `src/SvgCreator.Core/Diagnostics/DebugSnapshot*.cs`: デバッグスナップショットのモデルとシリアライザ
  - `src/SvgCreator.Core/Diagnostics/DebugSink.cs`: ファイル/Nullシンク実装
  - `src/SvgCreator.Core/Diagnostics/DebugDirectoryLayout.cs`: ファイル配置ヘルパー（ディレクトリ生成、ファイル名管理）

---
設計書完了。内容を確認して、次は `/sdd-tasks` を実行して実装タスクを作成してください。

## 採用選択肢（デフォルト）
- ライセンス方針: MIT優先、Apache-2.0許可、GPLは最終手段（避ける）。

【ライブラリ選定】
- 1) 画像処理/形態演算: OpenCvSharp（Apache-2.0）
- 2) 数値計算/最適化: Math.NET Numerics（MIT）＋必要箇所は自前L-BFGS
- 3) グラフ（SCC/トポソート）: 自前の最小DAGユーティリティ（MIT）
  - 選定理由（QuikGraphを採用しない）:
    - ライセンスがMS-PLでMIT/Apacheに比べ優先度が下がる
    - 必要機能（SCC/トポソート）が小規模実装で足り、依存削減と制御性が向上
    - スコア同点時の解決や閉路解消ポリシーを仕様に合わせやすい
- 4) SVG入出力: 自前Writer（System.Xml.Linq, MIT）＋ Svg.Skiaで描画検証（MIT）
  - 選定理由（SVG.NETを採用しない）:
    - ライセンスがMS-PLで優先度が下がる
    - 本要件は最小限の要素のみ（<svg>/<g>/<path>）で十分で、DOMフルスタックは過剰
    - 出力最適化（レイヤ≤15KB、不要属性削減）を自前で厳密制御しやすい
- 5) K-means（量子化）: ML.NET（MIT）
- 6) CLI/進捗: System.CommandLine（MIT）＋ Spectre.Console（MIT）
- 7) ロギング: Microsoft.Extensions.Logging（MIT）

【アルゴリズム/仕様】
- 8) 色空間: CIELAB
- 9) 初期化: k-means++
- 10) 多相セグメンテーション: 当面OFF（後日追加）
- 11) 隣接判定: 8近傍＋共有境界長
- 12) 閉路解消: SCC縮約→重み付きトポロジカルソート
- 13) 遮蔽補完: スプライン最適化近似（曲率^2+弧長, Math.NET＋自前L-BFGS）
- 14) Bézier: 三次中心
- 15) 近似誤差: 最大法線距離
- 16) 単純化: Douglas–Peucker＋曲率保持
- 17) 並列化: Parallel.For/PLINQ
- 18) 中間成果保存: 既定OFF（`--debug` で JSON 保存 ON、`--debug-stages` で対象絞り込み、`--debug-dir` で保存先変更）
- 19) 浮動小数精度: 幾何double/画像float
- 20) 出力形態: 単一SVG＋--export-layerで分割

【代替（将来の有効化条件）】
- グラフ高度化が必要→ QuikGraph（MS-PL）をプラグインで追加
- 既存SVG資産の読み込みやフィルタ/グラデ対応→ SVG.NET（MS-PL）または SharpVectors（BSD-3）を追加
