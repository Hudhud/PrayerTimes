namespace Web.ViewModels
{
    public class CustomErrorViewModel
    {
        public string RequestId { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorCode { get; set; }
        public string Details { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
