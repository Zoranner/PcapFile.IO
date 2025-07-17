using System;

namespace KimoTech.PcapFile.IO
{
    /// <summary>
    /// 操作结果的统一封装类型
    /// </summary>
    /// <typeparam name="T">结果值类型</typeparam>
    public class PcapResult<T>
    {
        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// 操作失败时为true
        /// </summary>
        public bool IsFailure => !IsSuccess;

        /// <summary>
        /// 结果值（成功时有效）
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// 错误消息（失败时有效）
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// 错误代码（失败时有效）
        /// </summary>
        public PcapErrorCode? ErrorCode { get; }

        /// <summary>
        /// 异常信息（失败时可能有效）
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        /// <param name="value">结果值</param>
        protected PcapResult(T value)
        {
            IsSuccess = true;
            Value = value;
            ErrorMessage = null;
            ErrorCode = null;
            Exception = null;
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        /// <param name="errorMessage">错误消息</param>
        /// <param name="errorCode">错误代码</param>
        /// <param name="exception">异常信息</param>
        protected PcapResult(
            string errorMessage,
            PcapErrorCode? errorCode = null,
            Exception exception = null
        )
        {
            IsSuccess = false;
            Value = default(T);
            ErrorMessage = errorMessage;
            ErrorCode = errorCode;
            Exception = exception;
        }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        /// <param name="value">结果值</param>
        /// <returns>成功的Result实例</returns>
        public static PcapResult<T> Success(T value)
        {
            return new PcapResult<T>(value);
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        /// <param name="errorMessage">错误消息</param>
        /// <returns>失败的Result实例</returns>
        public static PcapResult<T> Failure(string errorMessage)
        {
            return new PcapResult<T>(errorMessage);
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        /// <param name="errorMessage">错误消息</param>
        /// <param name="errorCode">错误代码</param>
        /// <returns>失败的Result实例</returns>
        public static PcapResult<T> Failure(string errorMessage, PcapErrorCode errorCode)
        {
            return new PcapResult<T>(errorMessage, errorCode);
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        /// <param name="exception">异常信息</param>
        /// <returns>失败的Result实例</returns>
        public static PcapResult<T> Failure(Exception exception)
        {
            var errorCode = ExtractErrorCode(exception);
            return new PcapResult<T>(exception.Message, errorCode, exception);
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        /// <param name="errorMessage">错误消息</param>
        /// <param name="exception">异常信息</param>
        /// <returns>失败的Result实例</returns>
        public static PcapResult<T> Failure(string errorMessage, Exception exception)
        {
            var errorCode = ExtractErrorCode(exception);
            return new PcapResult<T>(errorMessage, errorCode, exception);
        }

        /// <summary>
        /// 获取值或默认值
        /// </summary>
        /// <param name="defaultValue">默认值</param>
        /// <returns>成功时返回Value，失败时返回defaultValue</returns>
        public T GetValueOrDefault(T defaultValue = default(T))
        {
            return IsSuccess ? Value : defaultValue;
        }

        /// <summary>
        /// 执行成功时的操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        /// <returns>当前Result实例</returns>
        public PcapResult<T> OnSuccess(Action<T> action)
        {
            if (IsSuccess && action != null)
            {
                action(Value);
            }

            return this;
        }

        /// <summary>
        /// 执行失败时的操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        /// <returns>当前Result实例</returns>
        public PcapResult<T> OnFailure(Action<string, PcapErrorCode?, Exception> action)
        {
            if (IsFailure && action != null)
            {
                action(ErrorMessage, ErrorCode, Exception);
            }

            return this;
        }

        /// <summary>
        /// 转换结果类型
        /// </summary>
        /// <typeparam name="TNew">新的结果类型</typeparam>
        /// <param name="mapper">转换函数</param>
        /// <returns>转换后的Result</returns>
        public PcapResult<TNew> Map<TNew>(Func<T, TNew> mapper)
        {
            if (IsFailure)
            {
                return PcapResult<TNew>.Failure(ErrorMessage, ErrorCode ?? PcapErrorCode.Unknown);
            }

            try
            {
                var newValue = mapper(Value);
                return PcapResult<TNew>.Success(newValue);
            }
            catch (Exception ex)
            {
                return PcapResult<TNew>.Failure(ex);
            }
        }

        /// <summary>
        /// 从异常中提取错误代码
        /// </summary>
        /// <param name="exception">异常</param>
        /// <returns>错误代码</returns>
        protected static PcapErrorCode? ExtractErrorCode(Exception exception)
        {
            return exception switch
            {
                PcapException pcapEx => pcapEx.ErrorCode,
                System.IO.FileNotFoundException => PcapErrorCode.FileNotFound,
                System.IO.DirectoryNotFoundException => PcapErrorCode.DirectoryNotFound,
                System.UnauthorizedAccessException => PcapErrorCode.InsufficientPermissions,
                System.IO.IOException => PcapErrorCode.Unknown,
                ArgumentException => PcapErrorCode.InvalidArgument,
                OutOfMemoryException => PcapErrorCode.OutOfMemory,
                _ => null,
            };
        }

        /// <summary>
        /// 隐式转换为bool（用于条件判断）
        /// </summary>
        /// <param name="result">Result实例</param>
        public static implicit operator bool(PcapResult<T> result)
        {
            return result?.IsSuccess ?? false;
        }

        /// <summary>
        /// 重写ToString方法
        /// </summary>
        /// <returns>结果的字符串表示</returns>
        public override string ToString()
        {
            if (IsSuccess)
            {
                return $"Success: {Value}";
            }
            else
            {
                var result = $"Failure: {ErrorMessage}";
                if (ErrorCode.HasValue)
                {
                    result += $" (Code: {ErrorCode})";
                }

                return result;
            }
        }
    }

    /// <summary>
    /// 无返回值的操作结果
    /// </summary>
    public class Result : PcapResult<object>
    {
        /// <summary>
        /// 创建成功结果
        /// </summary>
        protected Result()
            : base(null) { }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        /// <param name="errorMessage">错误消息</param>
        /// <param name="errorCode">错误代码</param>
        /// <param name="exception">异常信息</param>
        protected Result(
            string errorMessage,
            PcapErrorCode? errorCode = null,
            Exception exception = null
        )
            : base(errorMessage, errorCode, exception) { }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        /// <returns>成功的Result实例</returns>
        public static Result Success()
        {
            return new Result();
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        /// <param name="errorMessage">错误消息</param>
        /// <returns>失败的Result实例</returns>
        public new static Result Failure(string errorMessage)
        {
            return new Result(errorMessage);
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        /// <param name="errorMessage">错误消息</param>
        /// <param name="errorCode">错误代码</param>
        /// <returns>失败的Result实例</returns>
        public new static Result Failure(string errorMessage, PcapErrorCode errorCode)
        {
            return new Result(errorMessage, errorCode);
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        /// <param name="exception">异常信息</param>
        /// <returns>失败的Result实例</returns>
        public new static Result Failure(Exception exception)
        {
            var errorCode = ExtractErrorCode(exception);
            return new Result(exception.Message, errorCode, exception);
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        /// <param name="errorMessage">错误消息</param>
        /// <param name="exception">异常信息</param>
        /// <returns>失败的Result实例</returns>
        public new static Result Failure(string errorMessage, Exception exception)
        {
            var errorCode = ExtractErrorCode(exception);
            return new Result(errorMessage, errorCode, exception);
        }
    }
}
