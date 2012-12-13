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

namespace CPHFixes.Test.master2
{
    public partial class Master2 : System.Web.UI.MasterPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            //output TemplateControl.AppRelativeVirtualPath
            
            //The .Parent.Parent is to account for the <div runat="server">  we are using to apply the css.
            //Without the div, it would be .Parent
            lbl1.Text = lbl1.Parent.Parent.TemplateControl.AppRelativeVirtualPath;
            if (lbl1.Text == this.AppRelativeVirtualPath) div1.Attributes.Add("class", "ok");
            else div1.Attributes.Add("class", "error");
            System.Diagnostics.Debug.Assert(lbl1.Parent.Parent is ContentPlaceHolder);

            System.Diagnostics.Debug.Assert(!(lbl2.Parent.Parent is ContentPlaceHolder));
            lbl2.Text = lbl2.Parent.Parent.TemplateControl.AppRelativeVirtualPath;
            if (lbl2.Text == this.AppRelativeVirtualPath) div2.Attributes.Add("class", "ok");
            else div2.Attributes.Add("class", "error");

            System.Diagnostics.Debug.Assert(lbl3.Parent.Parent is ContentPlaceHolder);
            lbl3.Text =  GetAdjustedParentTemplateControl(lbl3.Parent.Parent).AppRelativeVirtualPath;
            if (lbl3.Text == this.AppRelativeVirtualPath) div3.Attributes.Add("class", "ok");
            else div3.Attributes.Add("class", "error");
        }
        /// <summary>
        /// Returns the adjusted TemplateControl property for 'c'. Accounts for the ContentPlaceHolder 
        /// Template Control bug.
        /// 
        /// This bug causes the TemplateControl property of each ContentPlaceHolder to equal the master
        /// page that the ContentPlaceHolder originated from. Unfortunately, the contents of each 
        /// matchingContent control are dumped right into the ContentPlaceHolder. This makes it 
        /// impossible to rely on Parent.TemplateControl, because if the control is right inside 
        /// a Content tag, then it will evaluate to the Master page, instead of the Content page.
        /// 
        /// This function should be useful for .ascx controls wishing to find their true TemplateControl
        /// parent. 
        /// </summary>
        /// <param name="c">The control you want to calculate the adjusted parent TemplateControl
        /// property for. If c *is* a TemplateControl, then the function will return the 
        /// parent TemplateControl</param>
        /// <returns></returns>
        public static TemplateControl GetAdjustedParentTemplateControl(Control c)
        {
            Control p = null;

            if (c is ContentPlaceHolder)
            {
                //So this method can find the right value for a ContentPlaceHolder itself.
                //To make things work right, CPHs should act as members of the content page, 
                //since the CONTENT tags are replaced by them.
                p = c;
            }
            else
            {
                //We can't do anything here - We must have a parent if c isn't a ContentPlaceHolder.
                if (c.Parent == null) return c.TemplateControl;

                //Start with the parent
                //We want to skip c, so we can use this function on TemplateControls directly 
                //as well as on their .Parent attribute.
                p = c.Parent;

                //Find the first ContentPlaceHolder or TemplateControl in the ancestry. 
                //We skipped c above (also stops at the root control)
                while ((p.Parent != null) && !(p is ContentPlaceHolder) && !(p is TemplateControl))
                    p = p.Parent;

                //If there aren't any CPHs in the immediate heirarchy, we have nothing to adjust for
                //(An intermediate TemplateControl (.ascx file, UserControl) makes it safe, since it
                //overrides the TemplateControl, etc. properties)
                if (!(p is ContentPlaceHolder)) return c.TemplateControl;
            }



            //If the TemplateControl properties match, then we need to fix the child's (The child's should
            // reference the child TemplateControl instead). If they're different, we have nothing to correct.
            if (p.TemplateControl != c.TemplateControl)
            {
                return c.TemplateControl; //Hey, it's different - maybe an intermediate PlaceHolder is
                //cleaning things up for us.
            }
            else
            {
                //At this point we know that 'c' has an invalid TemplateControl value, because
                //it *must* be different from the value the parent CPH has.

                //At this point, 'c' must be inside a content page or a intermediate 
                //master page.

                //We also know that the correct value is the child TemplateControl
                //So we start at 'c' and work our way back through the master page chain. 
                //We will return the child right before the match.

                //We're starting at the content page and then going through the master pages.

                //Return the content page if the immediate master page is a match
                if (c.Page.Master == c.TemplateControl) return c.Page;

                //Loop through the nested master pages
                MasterPage mp = c.Page.Master;
                while (true)
                {
                    System.Diagnostics.Debug.Assert(mp.Master != null,
                      "How can the CPH have a TemplateControl reference that's not in the heirarchy?");

                    //If the parent is a match, return the child.
                    if (mp.Master == c.TemplateControl) return mp;
                    //No match yet? go deeper
                    mp = mp.Master;

                }
            }
        }

    }
}
