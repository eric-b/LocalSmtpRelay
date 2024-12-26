using System;

namespace LocalSmtpRelay.Model
{
    [Flags]
    public enum MessageField
    {
        None = 0,
        Subject = 1 << 0,
        Body = 1 << 1
    }
}
