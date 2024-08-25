
namespace LocalSmtpRelay.Model
{
    public sealed class SendResponse 
    {
        public bool IsSuccess { get; }

        public SendResponse(bool isSuccess)
        {
            IsSuccess = isSuccess;
        }
    }
}
