namespace PrintApp.Models;

// Cho phép mọi lỗi Sakura mang theo 1 mã lỗi (Code) + tham số (Params) thay vì chỉ
// 1 câu chữ cố định — để front-end tự dịch theo ngôn ngữ đang chọn (EN/ZH) thay vì
// hiển thị nguyên văn tiếng Việt hardcode. Message vẫn giữ tiếng Việt làm fallback/log.
public interface ISakuraCodedException
{
    string Code { get; }
    object? Params { get; }
}

// Kế thừa ArgumentException để các catch (ArgumentException) hiện có vẫn bắt đúng
// (giữ nguyên status code 400), chỉ bổ sung Code/Params.
public class SakuraValidationException : ArgumentException, ISakuraCodedException
{
    public string Code { get; }
    public object? Params { get; }

    public SakuraValidationException(string code, string message, object? errorParams = null) : base(message)
    {
        Code = code;
        Params = errorParams;
    }
}

// Kế thừa InvalidOperationException để giữ nguyên status code 409 ở các catch hiện có.
public class SakuraConflictException : InvalidOperationException, ISakuraCodedException
{
    public string Code { get; }
    public object? Params { get; }

    public SakuraConflictException(string code, string message, object? errorParams = null) : base(message)
    {
        Code = code;
        Params = errorParams;
    }
}

// Dùng cho lỗi không cần gắn với 1 status code cụ thể qua kiểu exception (vd lỗi cấu hình).
public class SakuraCodedException : Exception, ISakuraCodedException
{
    public string Code { get; }
    public object? Params { get; }

    public SakuraCodedException(string code, string message, object? errorParams = null) : base(message)
    {
        Code = code;
        Params = errorParams;
    }
}
