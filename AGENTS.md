# 作業時に遵守しているルールまとめ

## ツール・ファイル編集
- 既存ファイルの変更や新規作成には基本的に `apply_patch` を用いる。
- ファイルを直接上書きする必要が生じる場合は、開発ルールに矛盾しないか確認した上で最小限に留める。
- コード解析や象徴操作を行う際は Serena ツール群を初期化のうえ活用する。

## コードスタイルとコメント
- C# コードには XML ドキュメントコメント（`/// <summary>...</summary>` 等）を公開 API に付与する。
- コメントは必要最低限とし、処理意図や仕様背景など読み手が迷いそうな箇所に限定する（自明な処理には付与しない）。
- デバッグ関連クラスなど仕様と密接な箇所では、`design.md` と整合する説明を入れる。
- フォーマットは .NET 標準（`dotnet format` 相当）を意識し、命名規則は PascalCase / camelCase を徹底する。
- UTF8-BOMに統一
## テストと検証
- コード変更後は必ず関連テスト（`.NET` プロジェクトでは `dotnet test`）を実行し、成功を確認する。
- テストを実行した場合、その事実とコマンド名を報告に含める。
- テストコードには以下のようにどのようなテストなのかをコメントに必ず記載する

```cs
// 生成された SVG のルート要素が仕様通りの寸法・メタ属性を持つことを確認する。
[Fact]
public void EmitDocument_RootContainsExpectedAttributes()
{
    var image = CreateImage(128, 256);
    var geometry = CreateRectangleGeometry("layer-001", new RgbColor(10, 20, 30));
    var depthOrder = CreateDepthOrder(("layer-001", 0));

    var emitter = new SvgEmitter();
    var svg = emitter.EmitDocument(image, new[] { geometry }, depthOrder);

    var document = XDocument.Parse(svg);
    var root = document.Root ?? throw new InvalidOperationException("SVG root element is missing.");

    Assert.Equal("svg", root.Name.LocalName);
    Assert.Equal(Svg.NamespaceName, root.Name.NamespaceName);
    Assert.Equal("128", root.Attribute("width")?.Value);
    Assert.Equal("256", root.Attribute("height")?.Value);
    Assert.Equal("0 0 128 256", root.Attribute("viewBox")?.Value);
    Assert.Equal("1.1", root.Attribute("version")?.Value);
    Assert.Equal("SvgCreator", root.Attribute("data-generator")?.Value);
}
```

## タスク進行
- `.sdd/specs/.../tasks.md` に定義されたタスクは順番に実施し、完了した項目はチェックマークを更新する。
- 各タスクは TDD（RED→GREEN→REFACTOR）で進めることを基本とし、途中でタスクを飛ばさない。
- 各タスクは一気に行わずこまめにgit commitすること
- 各タスクを完了したタイミングで必ずコミットし、履歴を細かく残すことを厳守する。

## ドキュメント同期
- 要件や設計を更新した場合、対応するタスクリストや実装計画も必ず同期させる。
- デバッグ機能など仕様変更があった時は、`requirements.md`・`design.md`・`tasks.md` を一貫した内容に保つ。

## レポート
- 実装内容を報告する際は、変更点の概要・編集ファイルの主な位置・実行したテスト結果・次の候補ステップを簡潔にまとめる。
- コマンド実行結果は必要に応じてサマリで共有し、ログ全文の貼り付けは避ける。
