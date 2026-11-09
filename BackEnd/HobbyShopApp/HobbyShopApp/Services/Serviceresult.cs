namespace HobbyShopApp.Services;

// Các loại lỗi service có thể trả về. Controller dựa vào đây để chọn mã HTTP phù hợp.
public enum ServiceErrorType
{
    None,
    Validation,   // dữ liệu không hợp lệ        -> 400
    NotFound,     // không tìm thấy               -> 404
    Conflict      // xung đột (trùng, đang dùng)  -> 409
}

// Kết quả của một thao tác nghiệp vụ: hoặc thành công kèm dữ liệu, hoặc thất bại kèm loại lỗi và thông báo.
public class ServiceResult<T>
{
    public ServiceErrorType ErrorType { get; private set; } = ServiceErrorType.None;
    public bool Success => ErrorType == ServiceErrorType.None;
    public T? Data { get; private set; }
    public string? Error { get; private set; }

    public static ServiceResult<T> Ok(T data) => new() { Data = data };

    public static ServiceResult<T> Fail(ServiceErrorType type, string error) =>
        new() { ErrorType = type, Error = error };
}