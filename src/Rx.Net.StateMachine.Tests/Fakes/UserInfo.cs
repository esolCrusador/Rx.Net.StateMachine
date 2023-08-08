using System.Text;

namespace Rx.Net.StateMachine.Tests.Fakes
{
    public class UserInfo
    {
        public long UserId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Username { get; set; }

        public string Name
        {
            get
            {
                var result = new StringBuilder();
                if(FirstName != null)
                    result.Append(FirstName);
                if(LastName != null)
                {
                    result.Append(" ");
                    result.Append(LastName);
                }
                if (result.Length == 0)
                    result.Append(Username ?? UserId.ToString());

                return result.ToString();
            }
        }
    }
}
