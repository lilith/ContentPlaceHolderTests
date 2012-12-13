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

namespace CPHFixes.Test.master1
{
    public partial class Master1 : System.Web.UI.MasterPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            form1.Action = ResolveUrl("~/"); //Force a post-back to the real page
            DoWork();
        }
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

        }
        private void DoWork()
        {
            //We have to do this ugly hack so the checkbox will still work through a Server.Transfer
            String chk = this.Request.Form["ctl00$ctl00$chkPatch"];
            lblRepairTime.Text = "[not enabled]";
            //We're doing it late so chkPatch will be set
            if (chk != null && chk.Equals("on", StringComparison.OrdinalIgnoreCase))
            {
                System.Diagnostics.Stopwatch s = new System.Diagnostics.Stopwatch();
                s.Start();
                ContentPlaceHolderFixes.RepairPageHeader(Page);
                s.Stop();
                lblRepairTime.Text = s.Elapsed.TotalMilliseconds.ToString() + "ms";
                chkPatch.Checked = true;
                timeInfo.Visible = true;
            }

            //Translate meta tags into css

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<style type=\"text/css\">");

            List<HtmlMeta> meta = ContentPlaceHolderFixes.GetControlsOfType<HtmlMeta>(Page.Header);
            foreach (HtmlMeta m in meta)
            {
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

    /// <summary>
    /// A HtmlControl which resolves the 'src' attribute at render time.
    /// </summary>
    public class ScriptReference : HtmlControl
    {
        public ScriptReference()
            : base("script")
        { }


        protected override void Render(HtmlTextWriter writer)
        {
            //Self-closing script tags corrupt the DOM in FF
            writer.WriteBeginTag(this.TagName);
            this.RenderAttributes(writer);
            writer.Write(">");
            writer.WriteEndTag(this.TagName);
        }

        protected override void RenderAttributes(HtmlTextWriter writer)
        {
            if (!string.IsNullOrEmpty(this.Src))
            {
                base.Attributes["src"] = base.ResolveClientUrl(this.Src);
            }
            base.RenderAttributes(writer);
        }

        // Properties
        [UrlProperty, DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), DefaultValue("")]
        public virtual string Src
        {
            get
            {
                string str = base.Attributes["src"];
                if (str == null)
                {
                    return string.Empty;
                }
                return str;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) base.Attributes["src"] = null;
                else base.Attributes["src"] = value;
            }
        }
    }
    /// <summary>
    /// Provides utility methods to clean up after 2 separate bugs related to ContentPlaceHolders.
    /// Bug 1) ContentPlaceHolders inside the head section prevent stylesheets and meta tags from being parsed correctly. Use RepairPageHeader(page) to reparse and fix the hierarchy.
    /// Bug 2) ContentPlaceHolders report their TemplateControl as their originating MasterPage, instead of the Content page their contents come from. Use GetAdjustedParentTemplateControl(control) to calculate the right value.
    /// 
    /// </summary>
    public static class ContentPlaceHolderFixes
    {



        /// <summary>
        /// Matches HTML, ASP comments and Tags with no contents. We didn't want to match tags inside comments.
        /// We spit the comments back out, but parse the tags, looking for link, meta, and script.
        /// We are doing post-proccessing only on literal tags, so everything else that needs to be parsed already should be.
        /// 
        /// </summary>
        private static readonly Regex CommentsAndSingleTags = new Regex("(?:(?<comment>(?:<!--.*?-->|<%--.*?--%>))|<(?<tagname>[\\w:\\.]+)(\\s+(?<attrname>\\w[-\\w:]*)(\\s*=\\s*\"(?<attrval>[^\"]*)\"|\\s*=\\s*'(?<attrval>[^']*)'|\\s*=\\s*(?<attrval><%#.*?%>)|\\s*=\\s*(?<attrval>[^\\s=/>]*)|(?<attrval>\\s*?)))*\\s*(?:(?<selfclosing>/>)|>\\s*</(?<endtagname>[\\w:\\.]+)\\s*>))",
                     RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.Multiline);


        /// <summary>
        /// Reparses meta and link tags that weren't parsed correctly the first time.
        /// 
        /// 1) Parses literal controls: looks for self-closing and empty meta, link, and script controls. 
        /// Supports Visible and EnableViewState attributes correctly. EnableViewState=false if not specified.
        /// The TemplateControl is re-calculated.
        /// 2) Parses HtmlGenericControls (link, meta, script tags with runat="server"). HtmlGenericControl instances for 'script' can
        /// only exist if added through code, since &lt;script runat="server"&gt; is for server side code. Supports it anyway :)
        /// Supports Visible and EnableViewState attributes correctly. 
        /// The TemplateControl is re-calculated.
        /// Note: script references don't have a built-in control, so we use a custom ScriptReference instance to provide the rebasing support.
        /// </summary>
        /// <param name="p"></param>
        public static void RepairPageHeader(Page p)
        {
            //Aquire a collection of references to each contentplaceholder in the Head section.
            //ContentPlaceHolders do not account for being located in an HtmlHead, and simply
            //perform the normal parsing logic. We will patch this by iterating through incorrectly identified tags such as
            //<link> and <meta>, and replacing them with the proper control type. 
            //As a bonus, we also make script references work, but that is an extra - Head doesn't usually do anything special
            //with those anyway.

            //Note that each contentplaceholder usually contains a child ContentPlaceHolder
            if (p.Header != null)
            {

                ////////////////////// Literal parsing //////////////////////////////
                //Get a collection of all of the LiteralControls in the head
                //This will handle link, meta, and script includes
                List<LiteralControl> toParse = GetControlsOfType<LiteralControl>(p.Header);

                //handle literal links
                if (toParse != null)
                {
                    foreach (LiteralControl lc in toParse)
                    {
                        //if the literal is directly inside a content tag, on a content page, the TemplateControl property will
                        //incorrectly point to the MasterPage.
                        //So if we use lc.AppRelativeTemplateSourceDirectory, it doesn't work
                        //However, if we use this.AppRelativeTemplateSourceDirectory and
                        //we have a MasterPage>MasterPage(with relative literal stylesheet reference)>Page
                        //Then the relative stylesheet reference will be broke, relative to the Page.

                        //The solution is to find the child TemplateControl of lc's nearest ContentPlaceHolder parent.
                        //We do this before ParseLiteral control, because this value will be propogated to the
                        //component controls.
                        lc.TemplateControl = ContentPlaceHolderFixes.GetAdjustedParentTemplateControl(lc);

                        //Parse literal control
                        Control c = ParseLiteralControl(lc, lc.Text);
                        //notused:  //Article.SetPathRecursive(c, getAdjustedParent(lc).AppRelativeTemplateSourceDirectory);//used to be this.
                        //Replace 
                        lc.Parent.Controls.AddAt(lc.Parent.Controls.IndexOf(lc), c);
                        lc.Parent.Controls.Remove(lc);
                    }
                }

                //handle links with runat="server"
                //Get a collection of all of the HtmlGenericControls in the page header
                List<HtmlGenericControl> toFix = GetControlsOfType<HtmlGenericControl>(p.Header);
                if (toFix != null)
                {

                    //This will handle all link tags, meta tags, or script tags with runat="server"
                    //Also affects script tags parsed in above section (URL resolution)

                    //Iterate through the collection, replacing or modifying the neccesary objects.
                    foreach (HtmlGenericControl hgc in toFix)
                    {
                        HtmlControl replacement = null;
                        switch (hgc.TagName.ToLower())
                        {
                            case "link":
                                //Create a replacement HtmlLink object with identical attributes.
                                //HtmlLink will resolve virtual URLs on the href attribute at render-time, unlike HtmlGenericControl.
                                replacement = new HtmlLink();

                                break;
                            case "meta":
                                //Create a replacement HtmlMeta object with identical attributes.
                                replacement = new HtmlMeta();
                                break;

                            case "script":
                                //Create a new script reference, which resolves the src attribute at render-time
                                //impossible, unlessed 
                                replacement = new ScriptReference();
                                break;

                        }


                        if (replacement != null)
                        {
                            //Adjust the TemplateControl for the *other* ContentPlaceHolder bug.
                            replacement.TemplateControl = GetAdjustedParentTemplateControl(hgc.TemplateControl);

                            //Turn off ViewState
                            replacement.EnableViewState = false;

                            //Copy attributes
                            foreach (string s in hgc.Attributes.Keys)
                            {
                                string val = hgc.Attributes[s];
                                replacement.Attributes.Add(s, val);
                            }
                            //Assign known properties that aren.t collection-backed
                            replacement.EnableViewState = hgc.EnableViewState;
                            replacement.Visible = hgc.Visible;

                            //Insert the new object next to the old, then remove the old.
                            hgc.Parent.Controls.AddAt(hgc.Parent.Controls.IndexOf(hgc), replacement);
                            hgc.Parent.Controls.Remove(hgc);
                        }
                    }
                }

            }


            /* Analyize TemplateControls. Prints TemplateControl/TemplateSourceDirectory tree for diagnostics.
                List<Control> allcontrols = GetControlsOfType<Control>(this);
                List<TemplateControl> uniqueTemplateControls =new List<TemplateControl>();
                Debug.WriteLine(Page.AppRelativeTemplateSourceDirectory);
                foreach (Control c in allcontrols)
                {
                    //Debug.WriteLine(c.ID + "   (" + c.TemplateControl.ID + ") - " + c.AppRelativeTemplateSourceDirectory);
                    if (!uniqueTemplateControls.Contains(c.TemplateControl)){
                        uniqueTemplateControls.Add(c.TemplateControl);
                        string s = c.TemplateSourceDirectory;
                    }
                    
                }
                StringWriter sw = new StringWriter();
                PrintTree(this.Header, 0, sw);
                this.Header.Controls.AddAt(0, new LiteralControl(Server.HtmlEncode((sw.ToString()))));
            */
        }

        /// <summary>
        /// Parses the specified literal control and returns a replacement control (often PlaceHolder) containing 
        /// the newly parsed controls. Only self-closing tags or tags that only contain whitespace. Creates
        /// HtmlMeta (for &lt;meta&gt;), HtmlLink (for &lt;link&gt;), HtmlGenericControl (for &lt;script&gt;) and uses LiteralControl instances for the rest
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        private static Control ParseLiteralControl(Control c, string text)
        {
            if (c.Controls.Count > 0) return c;
            PlaceHolder ph = new PlaceHolder();

            int strIndex = 0;

            while (strIndex < text.Length)
            {
                Match m = CommentsAndSingleTags.Match(text, strIndex);

                //We're at the end. Add the last literal and leave.
                if (!m.Success)
                {
                    if (text.Length > strIndex)
                    {
                        LiteralControl lastLiteral = new LiteralControl(text.Substring(strIndex, text.Length - strIndex));
                        ph.Controls.Add(lastLiteral);
                    }
                    break;
                }

                //We've hit another comment or tag. 
                //Add the text since the last match.
                int spaceSinceLastMatch = m.Index - strIndex;
                if (spaceSinceLastMatch > 0)
                {
                    LiteralControl inbetween = new LiteralControl(text.Substring(strIndex, spaceSinceLastMatch));//"<!--In between and before matches: (-->" + text.Substring(strIndex,spaceSinceLastMatch) + "<!--)-->");
                    ph.Controls.Add(inbetween);
                }

                //build control
                string matchText = text.Substring(m.Index, m.Length);
                if (m.Groups["comment"].Success)
                {
                    LiteralControl comment = new LiteralControl(matchText);//"<!--Comment:(-->" + matchText + "<!--)-->");
                    ph.Controls.Add(comment);
                }
                else if (m.Groups["tagname"].Success)
                {
                    if (m.Groups["endtagname"].Success)
                    {
                        if (!m.Groups["tagname"].Value.Equals(m.Groups["endtagname"].Value))
                        {
                            LiteralControl error = new LiteralControl("<!-- fbs Parser Error - end tag does not match start tag : -->" + matchText);
                            ph.Controls.Add(error);
                        }
                    }

                    //Parse tag

                    string tagname = m.Groups["tagname"].Value.Trim().ToLower();

                    //Store the attribute names and values into the attrs collection 
                    NameValueCollection attrs = new NameValueCollection();

                    Group anames = m.Groups["attrname"];
                    Group avals = m.Groups["attrval"];
                    if (anames != null && avals != null)
                    {
                        for (int i = 0; i < anames.Captures.Count; i++)
                        {
                            string name = anames.Captures[i].Value;
                            if (i < avals.Captures.Count)
                            {
                                string value = avals.Captures[i].Value;
                                attrs[name] = value;
                            }
                        }
                    }
                    if (tagname.Equals("link") ||
                        tagname.Equals("meta") ||
                        tagname.Equals("script"))
                    {
                        HtmlControl hc = null;
                        switch (tagname)
                        {
                            case "link":
                                hc = new HtmlLink();
                                break;
                            case "meta":
                                hc = new HtmlMeta();
                                break;
                            case "script":
                                hc = new ScriptReference();
                                break;
                        }

                        //Inherit TemplateControl value
                        hc.TemplateControl = c.TemplateControl;
                        //Copt attrs
                        foreach (string key in attrs.AllKeys)
                        {
                            hc.Attributes[key] = attrs[key];
                        }
                        //Apply attributes to known properties that aren't Attributes backed.
                        if (hc.Attributes["visible"] != null)
                        {
                            hc.Visible = hc.Attributes["visible"].Equals("true", StringComparison.OrdinalIgnoreCase);
                            hc.Attributes.Remove("visible");
                        }
                        if (hc.Attributes["EnableViewState"] != null)
                        {
                            hc.EnableViewState = hc.Attributes["EnableViewState"].Equals("true", StringComparison.OrdinalIgnoreCase);
                            hc.Attributes.Remove("EnableViewState");
                        }
                        else hc.EnableViewState = false;

                        ph.Controls.Add(hc);
                    }
                    else
                    {
                        //Just pass unrecognized text through
                        LiteralControl notRecognized = new LiteralControl(matchText);//"<!-- tag name not recognized: (-->" + matchText + "<!--)-->");
                        ph.Controls.Add(notRecognized);
                        break;
                    }

                }
                else
                {
                    LiteralControl regexError = new LiteralControl("<!-- regex error: (-->" + matchText + "<!--)-->");
                    ph.Controls.Add(regexError);
                    //Should never happen... Either group Comment or group TagName should be defined.
                }


                strIndex = m.Index + m.Length;

            }
            if (ph.Controls.Count == 1) return ph.Controls[0];
            else return ph;

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




        /// <summary>
        /// Iterates over the control structure of the specified object and returns all elements that are
        /// of the specified type
        /// </summary>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static List<T> GetControlsOfType<T>(Control parent) where T : Control
        {
            return GetControlsOfType<T>(parent, false, false);
        }
        /// <summary>
        /// Iterates over the control structure of the specified object and returns all elements that are
        /// of the specified type. If there are two items of the specified type, and one is a child of the other, 
        /// the childrenOnly and parentOnly parameters can be used to control which is selected. If both are false, both controls are returned.
        /// </summary>
        /// <param name="parent">The control to search</param>
        /// <param name="childrenOnly">If true, only the innermost matching children will be returned.</param>
        /// <param name="parentsOnly">If true, only the outermost matching parents will be returned.</param>
        /// <returns></returns>
        public static List<T> GetControlsOfType<T>(Control parent, bool childrenOnly, bool parentsOnly) where T : Control
        {
            if (parent == null) return null;
            if (childrenOnly && parentsOnly) throw
                new ArgumentException("Only one of childrenOnly and parentsOnly may be true. They are mutually exclusive");

            //We are doing last-minute initialization to minimize the overhead of building one of these.
            //The List<> constructor should only be called n times, where n is the number of ContentPlaceHolder controls.
            List<T> temp = null;

            if (parent.Controls != null)
            {
                //Loop through all of the child controls
                foreach (Control child in parent.Controls)
                {
                    //Recursively search them also.
                    List<T> next = GetControlsOfType<T>(child, childrenOnly, parentsOnly);

                    //To save on initialization costs.
                    if (next != null)
                    {
                        if (temp == null)
                        {
                            temp = next; //Use existing collection from recursive call
                        }
                        else
                        {
                            //Merge the collections

                            //If a the same object is the child of two different parents, this will
                            //stop it.
                            foreach (T c in next)
                            {
                                if (!temp.Contains(c)) temp.Add(c);
                            }

                        }
                    }
                }
            }

            //If this item is of the target type, add it 
            if ((parent is T))
            {
                //If there are no children or we are trying to discard children
                if (parentsOnly || temp == null)
                {
                    //Clear the list and add the parent
                    T item = (T)parent;

                    temp = new List<T>();

                    temp.Add(item);
                }
                else if (!childrenOnly)
                {
                    //Append the parent with the children
                    T item = (T)parent;

                    if (temp == null) temp = new List<T>();

                    temp.Add(item);
                }
            }

            return temp;
        }


    }
}
