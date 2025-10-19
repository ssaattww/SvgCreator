## Python実装 調査メモ（Image-Vectorization-with-Depth）

- 対象リポジトリ: marklaw1006/Image-Vectorization-with-Depth（Apache-2.0）。
- 主言語: Python 100%（GitHub表示）。
- 目的: SvgCreator への適用を見据え、公開Python実装のセットアップ、CLI、主要パラメータ、実行例、留意点を整理する。

### リポジトリ概要
- ディレクトリ: `IVYSO/` に実装、`pic/`（README 記載では `demo_pic/`）にサンプル画像。
- README には論文・著者・arXivリンク、方法の要旨、編集容易性の記述あり。

### セットアップ（README準拠）
- 推奨環境
  - Python: 3.8（conda 指示）
  - 依存: `numpy==1.24.3`, `numpy_indexed`, `numba==0.58.0`, `svgpathtools`, `svgwrite`, `scikit-image`, `networkx`, `tqdm`, `threaded`, `multiprocessing`, `pillow`, `scikit-learn`, `numba-progress`
- READMEのインストール手順（抜粋）
  - `conda create -n ivyso && conda activate ivyso` → `pip install ...`
- 実務メモ
  - `multiprocessing` は標準ライブラリ名のため、`pip install multiprocessing` は不要/非推奨のケースあり。
  - Python 3.8 固定は他プロジェクトと両立しづらい可能性。`uv` や `conda-lock` で環境分離を推奨。

### 実行方法とCLI引数（README準拠）
- 実行例
  - `cd IVYSO` → `python ivyso.py --image_path demo_pic/burger.png --output_path demo_pic --K 20 --seg_off`
- 主要オプション（README記述の要約）
  - `--image_path <path>`: 入力画像パス
  - `--output_path <dir>`: 出力フォルダ
  - `--K <int>`: K-means量子化の色数
  - `--noisy_threshold <int>`: 指定未満の画素数の連結成分をノイズ層とみなす閾値
  - `--seg_on / --seg_off`: 量子化グルーピングの有無（実画像向け/カートゥーン向け）
  - `--mu <float>`: 多相セグメンテーションの重み（小さいほど相フェーズ数が増加）
  - `--max_phases <int>`: 多相セグメンテーションの最大相数
  - `--seg_max_iter <int>`: 多相セグメンテーションの最大反復
  - `--delta <0..1>`: 深度順のしきい値
  - `--euler_max_iter <int>`: 各層のエラスティカ補完の最大反復
  - `--eps <float>`: 二重井戸ポテンシャルの ε
  - `--a <float>`: 弧長項の重み
  - `--b <float>`: 曲率項の重み
  - `--r <float>`: レベルセット抽出値
  - `--b_eps <float>`: Bézier 近似のフィッティング許容誤差
  - `--b_radius <float>`: 局所曲率極値の同定しきい値

### 実装スタイルの所見（README・依存関係からの推測）
- 数値計算は NumPy + Numba（JIT）主体。深層学習フレームワーク依存なし。
- グラフ操作は NetworkX、SVG出力は `svgwrite`／幾何処理は `svgpathtools` を併用。
- 進捗表示に `tqdm`、並列化にスレッド/マルチプロセス系のオプションあり。

### 論文との対応（節番号）
- `--delta`（深度順のしきい値）→ Sec. 2.1（Depth ordering energy）
- `--euler_max_iter`, `--a`, `--b`, `--eps` → Sec. 2.3（Elastica, Eq.(7)）, Sec. 4.2（Modified double-well）
- Bézier 近似の `--b_eps`, `--b_radius` → Sec. 2.4（SVG 近似）
- グローバル整序 → Sec. 2.2 / Sec. 5.3（凸包・トポロジカルソート）

### SvgCreator 取り込みメモ
- バージョン整合: 本実装は Python 3.8 想定。ツール枠での実行 or ライブラリ化を検討。
- パラメータ対比: 既存 `requirements.md` の `--quality`, `--delta`, `--threads`, `--export-layer` などにマップ可能（品質は K/反復/閾値のプリセットで表現）。
- 出力: `<g id="layer-XXX" data-depth="d">` 構造の採用と、レイヤ別15KB制約に向けた Bézier 簡略化（`--b_eps` 連動）調整。

### 既知の注意点
- READMEの `pip install multiprocessing` は標準ライブラリとの競合の可能性あり（必要に応じて削除）。
- `demo_pic/` と `pic/` の混在表記があるため、同梱サンプルの実パスは要確認。

### 参考文献・リンク
- GitHub: Image-Vectorization-with-Depth（README/ライセンス/実行例/依存関係）
  - https://github.com/marklaw1006/Image-Vectorization-with-Depth
- 論文: Image Vectorization with Depth（arXiv 2409.06648）
  - https://arxiv.org/html/2409.06648v1
  - https://arxiv.org/abs/2409.06648
