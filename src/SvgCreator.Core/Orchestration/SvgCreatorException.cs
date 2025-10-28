using System.Text;

namespace SvgCreator.Core.Orchestration;

/// <summary>
/// SvgCreator 専用の例外型です。エラーコードと推奨対処を含みます。
/// </summary>
public sealed class SvgCreatorException : Exception
{
    private SvgCreatorException(
        SvgCreatorErrorDescriptor descriptor,
        string message,
        string? details,
        Exception? innerException)
        : base(message, innerException)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        Details = details;
    }

    /// <summary>
    /// エラー記述子です。
    /// </summary>
    public SvgCreatorErrorDescriptor Descriptor { get; }

    /// <summary>
    /// エラーコードです。
    /// </summary>
    public SvgCreatorErrorCode ErrorCode => Descriptor.Code;

    /// <summary>
    /// エラーカテゴリです。
    /// </summary>
    public SvgCreatorErrorCategory Category => Descriptor.Category;

    /// <summary>
    /// 追加の詳細情報です。
    /// </summary>
    public string? Details { get; }

    /// <summary>
    /// 指定したコードから例外を生成します。
    /// </summary>
    /// <param name="code">エラーコード。</param>
    /// <param name="details">追加の詳細情報。</param>
    /// <param name="innerException">内部例外。</param>
    /// <returns>生成された例外。</returns>
    public static SvgCreatorException FromCode(
        SvgCreatorErrorCode code,
        string? details = null,
        Exception? innerException = null)
    {
        var descriptor = SvgCreatorErrorCatalog.GetDescriptor(code);
        var message = BuildMessage(descriptor, details);
        return new SvgCreatorException(descriptor, message, details, innerException);
    }

    private static string BuildMessage(SvgCreatorErrorDescriptor descriptor, string? details)
    {
        var builder = new StringBuilder();
        builder.Append(descriptor.Summary);

        if (!string.IsNullOrWhiteSpace(descriptor.RecommendedAction))
        {
            builder.Append(" Recommended action: ");
            builder.Append(descriptor.RecommendedAction);
        }

        if (!string.IsNullOrWhiteSpace(details))
        {
            builder.Append(" Details: ");
            builder.Append(details);
        }

        return builder.ToString();
    }
}
