using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QnABotAllFeatures
{
    public class LoginState
    {
        public LoginState(bool isLogin)
        {
            this.isLogin = isLogin;
        }
        public bool isLogin { get; set; }
    }

    
}
