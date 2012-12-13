using System;
using System.Data;
using System.Configuration;
using System.Collections;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Web.UI.HtmlControls;

namespace CPHFixes
{
    public partial class _Default : System.Web.UI.Page
    {
        protected override void OnPreInit(EventArgs e)
        {
            Server.Transfer("~/Test/page/page1.aspx");
            base.OnPreInit(e);
        }
        protected void Page_Load(object sender, EventArgs e)
        {
            
        }
    }
}
