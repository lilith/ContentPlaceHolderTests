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
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Collections.Specialized;

namespace CPHFixes.Test.master1 {
    public partial class Master1 : System.Web.UI.MasterPage {
        protected void Page_Load(object sender, EventArgs e) {
            form1.Action = ResolveUrl("~/"); //Force a post-back to the real page
            DoWork();
        }
        protected override void OnPreRender(EventArgs e) {
            base.OnPreRender(e);

        }
        private void DoWork() {
            //We have to do this ugly hack so the checkbox will still work through a Server.Transfer
            String chk = this.Request.Form["ctl00$ctl00$chkPatch"];
            lblRepairTime.Text = "[not enabled]";
            //We're doing it late so chkPatch will be set
            if (chk != null && chk.Equals("on", StringComparison.OrdinalIgnoreCase)) {
                System.Diagnostics.Stopwatch s = new System.Diagnostics.Stopwatch();
                s.Start();
                NathanaelJones.WebFormsFixes.ContentPlaceHolderFixes.RepairPageHeader(Page);
                s.Stop();
                lblRepairTime.Text = s.Elapsed.TotalMilliseconds.ToString() + "ms";
                chkPatch.Checked = true;
                timeInfo.Visible = true;
            }

            //Translate meta tags into css

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<style type=\"text/css\">");

            List<HtmlMeta> meta = NathanaelJones.WebFormsFixes.ControlUtils.GetControlsOfType<HtmlMeta>(Page.Header);
            foreach (HtmlMeta m in meta) {
                sb.AppendLine(".meta-" + m.Name + "-showthis {display:block !important;}");
                sb.AppendLine(".meta-" + m.Name + "-hidethis {display:none;}");
            }

            sb.AppendLine("</style>");
            metaCss.Text = sb.ToString();
            metaCss.Mode = LiteralMode.PassThrough;

            //output TemplateControl.AppRelativeVirtualPath
            //The .Parent.Parent is to account for the <div runat="server">  we are using to apply the css.
            //Without the div, it would be .Parent

            lbl1.Text = lbl1.Parent.Parent.TemplateControl.AppRelativeVirtualPath;
            if (lbl1.Text == this.AppRelativeVirtualPath) div1.Attributes.Add("class", "ok");
            else div1.Attributes.Add("class", "error");

            lbl2.Text = lbl2.Parent.Parent.TemplateControl.AppRelativeVirtualPath;
            if (lbl2.Text == this.AppRelativeVirtualPath) div2.Attributes.Add("class", "ok");
            else div2.Attributes.Add("class", "error");


        }
    }
}