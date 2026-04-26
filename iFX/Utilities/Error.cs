using System;

namespace ModelDebate.iFx.Utilities;

public class Error
{
    #region Members

    public string Code        { get; }
    public string Description { get; }

    #endregion

    #region C'tor

    public Error(string code, string description)
    {
        Code        = code;
        Description = description;
    }

    public Error(Exception exception)
    {
        Code        = "Exception";
        Description = $"Message: {exception.Message}; StackTrace: {exception.StackTrace}";
    }

    #endregion

    #region Public

    public override string ToString()
    {
        return $"[{Code}] {Description}";
    }

    #endregion
}
