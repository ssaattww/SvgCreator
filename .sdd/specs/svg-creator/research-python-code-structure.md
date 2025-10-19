## Python実装 コード構造とコールグラフ（IVYSO）

- 対象: `marklaw1006/Image-Vectorization-with-Depth` の `IVYSO/` ディレクトリ直下の Python 実装。
- 目的: 設計時に参照できるコード地図、処理段階、引数とデータフローの対応を整理する。
- 備考: 本資料は既に取得済みのソースのエントリーポイント（`ivyso.py`）と公開 README の情報を基にまとめている。

### エントリーポイントと処理段階
`IVYSO/ivyso.py` は CLI を受け取り、次の順で処理する。

1) Quantization（減色）
- `quantizer(pic, K, ...)` → `quantize()` → `(pic, colors, layers)` を返す。
- `Q.check_out(output_path, pic, colors, layers)` で中間成果を保存。

2) Grouping Quantization（任意）
- `seg_switch` が有効なら `Mult_Seg(pic, mu, max_phase, max_iter).new_segment()`。
- `M.save(join(output_path, "coarse_seg_result"))`。

3) Shape Layer 構築
- `shape_layer_factory(pic, layers, colors, shape_layer_params, coarse_seg_result, use_seg_result, auto_denoise)`
- `S.check_out(join(output_path, "shape_layers"), noisy_layer_path=join(output_path, "noisy_layers"))`

4) Depth Ordering（前後関係の整序）
- `shape_layer_order(D, params={"threshold": delta})` → `S.order()` → `S.check_out(...)`

5) Phase Construction
- `phase_factory(D, params={"r": 5, "thre": .5, "area_threshold": 30})` → `build_all_phases()` → `save(...)`

6) Occlusion Completion（エラスティカ最小化）
- `euler(D, params={"max_iter": euler_max_iter, "eps": eps, "a": a, "b": b, ...}, bezier_prep_param={"r": r})`
- `E.parallel_solve(solve_parallel=True, use_partial_convex_init=False)` → `E.save(...)`

7) Bézier 近似と SVG 出力
- `Bezier_Wrapper(D, k=6, circle_radius=b_radius, eps=b_eps, use_weighted=False)`
- `fit_all_colors()` → `ret = check_out()`
- `svg_helper(name, ret, h, w).write_svg()` で `ivyso_output.svg` を生成。

### 主要モジュールと役割
- `lib.quantizer`
  - `quantizer` クラス: 減色とノイズ処理、`quantize()`、`check_out()`。
  - 付随関数: 幾何・差分・ポテンシャル（`W`, `W_prime`, `laplacian` 等）を含むヘルパ。
- `lib.mult_seg`
  - `Mult_Seg`: 多相セグメンテーション。`new_segment()`, `save()` を提供。
- `lib.shape_layer`
  - `shape_layer`, `shape_layer_factory`: レイヤ表現と生成。`check_out()` で保存。
- `lib.shape_layer_ordering`
  - `shape_layer_order`: `order()` により深度順整序、`check_out()` で保存。
- `lib.shape_layer_phase_builder`
  - `phase_builder`, `phase_factory`: フェーズ生成（`build_all_phases()`, `save()`）。
- `lib.shape_layer_euler`
  - `euler`: エラスティカ系の最適化。`parallel_solve()` と `save()`。
- `lib.shape_layer_bezier_fitter`
  - `Bezier_fitter`, `Bezier_Wrapper`, `svg_helper`: 曲線近似と SVG 生成。
- `lib.shape_layer_PCI`
  - `Partial_Convex_Init`: エラスティカの初期化（省略可能）。
- `lib.new_helper`
  - `inverse_color_transform`, `get_boundary` などの補助。
- `lib.helper`
  - 幾何ヘルパ・保存/読込（`save_data`, `load_data`）。
- `lib.dilator`
  - 形態学的膨張等の補助ユーティリティ。

### コールグラフ（高レベル）
- `main(ivyso.py)`
  - → `quantizer.quantize()` → `quantizer.check_out()`
  - → `Mult_Seg.new_segment()` → `Mult_Seg.save()`（任意）
  - → `shape_layer_factory(...).check_out()`
  - → `shape_layer_order.order()` → `.check_out()`
  - → `phase_factory.build_all_phases()` → `.save()`
  - → `euler.parallel_solve()` → `.save()`
  - → `Bezier_Wrapper.fit_all_colors()` → `.check_out()` → `svg_helper.write_svg()`

### CLI 引数と段階の対応表（要点）
- 減色: `--K`, `--noisy_threshold`。
- グルーピング: `--seg_on/--seg_off`, `--mu`, `--max_phases`, `--seg_max_iter`。
- 整序: `--delta`（深度順しきい値）。
- 補完: `--euler_max_iter`, `--eps`, `--a`, `--b`, `--r`。
- Bézier: `--b_eps`, `--b_radius`。

### データフロー（ファイル/メモリ）
- `check_out()/save()` は中間成果を `output_path` 配下に保存。
  - 典型キー: `pic`, `layers`, `colors`, `shape_layers`, `noisy_layers`。
- 最終成果: `ivyso_output.svg`（`<g id="layer-XXX" data-depth="d">` 構造）。

### SvgCreator への適用ポイント
- レイヤ単位の中間成果（`shape_layers`, `noisy_layers`）を橋渡し形式に変換し、C# 側で SVG Path 生成/結合。
- 既存要件（1レイヤ ≤15KB、部分出力、CLI 互換）に合わせ、Bézier 近似誤差（`--b_eps`）をプリセットで制御。
- `--quality` プロファイルで `K/反復/しきい値` を束ねると UX が向上。

### 留意事項
- `Partial_Convex_Init` は既定では無効。品質/速度トレードオフの検証余地あり。
- 並列化（`parallel_solve`）で CPU 使用が増加。`--threads` 相当の外部制御が望ましい。
- 中間保存形式は Pickle 系。C# 連携時は JSON/NPY 変換の検討。

### 次アクション
- 各モジュールの公開メソッド一覧と引数/戻り値の精査（必要ならコード参照部分を追補）。
- 代表画像セットでステージ毎の成果を保存し、サイズ/品質/速度を測定。
