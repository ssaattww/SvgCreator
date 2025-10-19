## Python依存とC#移植候補（ライブラリ調査）

目的: Python実装（IVYSO）の依存関係を洗い出し、SvgCreatorのC#移植における代替・対応候補を提示する（ライセンス含む）。

### 出典
- IVYSO リポジトリ README の Installation/Run 記述（依存パッケージ列挙、Apache-2.0 ライセンス）。
- C# 候補ライブラリの公式ドキュメント/リポジトリ（各項目の末尾に出典）。

---

### Python実装が利用する主なライブラリ（README記載）
- 数値/加速: `numpy==1.24.3`, `numba==0.58.0`, `numpy_indexed`
- 画像/形態: `scikit-image`, `pillow`
- グラフ: `networkx`
- ベクタ/SVG: `svgpathtools`, `svgwrite`
- 学習/クラスタ: `scikit-learn`（KMeans 等）
- 付帯: `tqdm`, `threaded`, `multiprocessing`, `numba-progress`

注: `svgwrite` は主にSVG生成に用いる軽量ライブラリ。C#側では用途に応じてより広範なSVG APIを持つパッケージを選択。

---

### C#移植における代表候補（用途別）

- 数値計算/最適化（NumPy/Numba 代替）
  - Math.NET Numerics（数値計算/最適化・MIT）。ベクトル/行列、最適化、補間など広範。
  - ALGLIB for C#（L-BFGS 等の最適化・商用/無償版あり）。エラスティカ近似のパラメータ最適化などで有用。
  - 備考: Numba 相当のJITはC#ではRyuJITと値型/Span、`System.Numerics` のSIMD活用で代替。

- 画像処理/形態演算（scikit-image 相当）
  - OpenCvSharp（OpenCVの.NETバインディング・Apache-2.0）。モルフォロジー/連結成分/輪郭抽出を網羅。
  - SkiaSharp（2D描画・MIT）。描画や簡易フィルタ、後段のSVGレンダとの親和性。

- グラフ構造/アルゴリズム（NetworkX 相当）
  - QuikGraph（グラフ構造とアルゴリズム。MS-PL）。DAG化/トポロジカルソート/最短路など。

- ベクタ/SVG（svgpathtools・svgwrite 相当）
  - SVG.NET（読み書き/描画・MS-PL）。SVG 1.1対応のC#実装。
  - SharpVectors（SVG DOM/変換・BSD-3）。WPF/XAML 連携が強い。
  - Svg.Skia（SkiaSharp backend・MIT）。Skia と組み合わせてレンダ/画像化。

- クラスタリング/KMeans（scikit-learn 相当）
  - Accord.NET（KMeans/MiniBatchKMeans 等・LGPL-2.1）。用途を限定しラップ層で隔離。代替として ML.NET の `KMeansTrainer` も選択肢。

- CLI/進捗表示（tqdm 相当）
  - Spectre.Console（進捗/表/ツリー等・MIT）。

---

### ライセンス要点（候補の抜粋）
- OpenCvSharp: Apache-2.0
- Math.NET Numerics: MIT
- QuikGraph: MS-PL
- SVG.NET: MS-PL
- SharpVectors: BSD-3-Clause
- Svg.Skia: MIT
- Accord.NET: LGPL-2.1（コアDLLはLGPL、別DLLで異なるライセンスの場合あり）
- SkiaSharp: MIT
- ImageSharp: 現行は Six Labors Split License（商用は要ライセンス）。採用時は用途と整合を要確認。

---

### マッピング（実装観点の提案）
- 量子化（KMeans）: Accord.NET or ML.NET → `OpenCvSharp`の色空間変換（任意）→ `Math.NET`/LINQ で色中心更新。
- 連結成分/小領域除去: `OpenCvSharp` の `connectedComponents*` / `morphologyEx`（開閉）で置換。
- 深度順グラフ/整序: `QuikGraph` でDAG化→トポロジカルソート。必要に応じてフィードバック弧近似は自前実装。
- エラスティカ近似（曲率+弧長）: 目的関数を `Math.NET`/`ALGLIB` の最適化に委譲（L-BFGS等）。
- Bézier近似/パス出力: 制御点最適化→ `SVG.NET` で `<path d="...">` を生成。Skiaレンダが必要なら `Svg.Skia`/`SkiaSharp`。
- 進捗・ログ: `Spectre.Console` の Progress API。

---

### 選定ガイド（実務上の勘所）
- ライセンス整合: 商用品質で ImageSharp を使う場合は Split License を精査。MIT/Apache 系を優先（OpenCvSharp, SkiaSharp, Math.NET 等）。
- 保守性: Accord.NET は保守状況に留意（用途限定での採用＋代替の用意）。
- パフォーマンス: 画像前処理は OpenCV（C++ベース）を優先、重い最適化は ALGLIB/Math.NET を活用。C#側は並列化（`Parallel.For`/`TPL`）で Numba 相当を確保。

---

### 付記：Python側ライブラリの機能補足
- `svgpathtools`: パスの解析/交差/曲率/長さ等の幾何演算。C#では `SVG.NET` と自前幾何（`System.Numerics`/`NetTopologySuite`）の併用で代替可能。
- `svgwrite`: SVGの生成専用。C#は `SVG.NET`／`SharpVectors` の方が総合的。

---

### 参考リンク
- GitHub（IVYSO本体）: https://github.com/marklaw1006/Image-Vectorization-with-Depth
- 論文（arXiv v1 HTML）: https://arxiv.org/html/2409.06648v1
- NumPy: https://numpy.org/
- Numba: https://numba.pydata.org/
- scikit-image: https://scikit-image.org/
- scikit-learn: https://scikit-learn.org/
- NetworkX: https://networkx.org/
- svgpathtools: https://github.com/mathandy/svgpathtools
- svgwrite: https://github.com/mozman/svgwrite
- Math.NET Numerics: https://github.com/mathnet/mathnet-numerics
- ALGLIB for C#: http://www.alglib.net/
- OpenCvSharp: https://github.com/shimat/opencvsharp
- SkiaSharp: https://github.com/mono/SkiaSharp
- QuikGraph: https://github.com/KeRNeLith/QuikGraph
- SVG.NET: https://github.com/vvvv/SVG
- SharpVectors: https://github.com/ElinamLLC/SharpVectors
- Svg.Skia: https://github.com/wieslawsoltes/Svg.Skia
- Accord.NET: https://github.com/accord-net/framework
- ML.NET KMeans: https://learn.microsoft.com/dotnet/machine-learning/
- Spectre.Console: https://github.com/spectreconsole/spectre.console
- ImageSharp: https://github.com/SixLabors/ImageSharp
