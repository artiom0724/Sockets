namespace ClientSocket.Models
{
    public class ActionResult
    {
        public ActionResult()
        {
            FileSize = 0;
            TimeAwait = 0;
        }

        public long FileSize { get; set; }

        public long TimeAwait { get; set; }
    }
}