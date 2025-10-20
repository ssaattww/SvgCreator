# Technology Stack

## アーキテクチャ
- 暫定: CLI ベースの単一プロジェクト構成を想定。詳細は設計作業で確定予定。

## 使用技術
### 言語
- C# (.NET 8.0)

### フレームワーク
- 詳細は [design.md](.sdd/specs/svg-creator/design.md) を参照。

### 依存関係
- 暫定: フレームワーク構成に準拠。[design.md](.sdd/specs/svg-creator/design.md) に従って更新予定。

## 開発環境
### 必要なツール
- .NET SDK 8.0
- `dotnet` CLI
- Console Application テンプレート（`dotnet new console`）

### 推奨 IDE / エディタ
- 暫定: Visual Studio 2022 (17.10 以降) または Visual Studio Code + C# Dev Kit。

## コマンド運用
### ビルド
- 暫定: `dotnet build`

### テスト
- 暫定: `dotnet test`（テストプロジェクト作成後に更新予定）

### 実行
- 暫定: `dotnet run -- [オプション]`

## 設定・環境変数
- 暫定: 特になし。CLI オプションで制御予定。

## その他メモ
- 暫定: 設計ドキュメント更新後に追記。
