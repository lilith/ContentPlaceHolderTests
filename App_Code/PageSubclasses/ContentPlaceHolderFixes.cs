using System;
using System.Data;
using System.Configuration;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Web.UI.HtmlControls;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Collections.Generic;
namespace fbs
{
    public partial class PageBase : Page
    {
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
            /// </summary>
            private static readonly Regex CommentsAndSingleTags = new Regex("(?:(?<comment>(?:<!--.*?-->|<%--.*?--%>))|<(?<tagname>[\\w:\\.]+)(\\s+(?<attrname>\\w[-\\w:]*)(\\s*=\\s*\"(?<attrval>[^\"]*)\"|\\s*=\\s*'(?<attrval>[^']*)'|\\s*=\\s*(?<attrval><%#.*?%>)|\\s*=\\s*(?<attrval>[^\\s=/>]*)|(?<attrval>\\s*?)))*\\s*(?:(?<selfclosing>/>)|>\\s*</(?<endtagname>[\\w:\\.]+)\\s*>))",
                         RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.Multiline);




            /// <summary>
            /// Reparses meta and link tags that weren't parsed correctly the first time.
            /// Only works inside ContentPlaceHolder controls located inside the head.
            /// Uses 2 passes. pass 1 parses literal controls, pass 2 parses meta and link controls that had runat="server" (and were therefore parsed as HtmlGenericControls)
            /// Bonus feature! Script references inside ContentPlaceHolder controls are parsed into ScriptReference instances. Doesn't work outside of ContentPlaceHolders in &lt;head&gt;, though.
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
                    List<LiteralControl> toParse = PageBase.GetControlsOfType<LiteralControl>(p.Header);

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
                    //We just want the outermost cphs
                    List<ContentPlaceHolder> cphList = GetControlsOfType<ContentPlaceHolder>(p.Header, false, true);

                    if (cphList != null)
                    {
                        //There may be multiple CPHs in the head section
                        foreach (ContentPlaceHolder cph in cphList)
                        {

                            //Get a collection of all of the HtmlGenericControls in the current ContentPlaceHolder.
                            List<HtmlGenericControl> toFix = GetControlsOfType<HtmlGenericControl>(cph);
                            if (toFix == null) continue;

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
                                        replacement = new fbs.PageBase.ScriptReference();
                                        break;

                                }


                                if (replacement != null)
                                {
                                    //Adjust the TemplateControl for the *other* ContentPlaceHolder bug.
                                    replacement.TemplateControl = GetAdjustedParentTemplateControl(hgc.TemplateControl);

                                    //Copy attributes
                                    foreach (string s in hgc.Attributes.Keys)
                                    {
                                        string val = hgc.Attributes[s];
                                        replacement.Attributes.Add(s, val);
                                    }
                                    //Assign known properties that aren.t collection-backed
                                    if (replacement.Attributes["visible"] != null)
                                    {
                                        replacement.Visible = replacement.Attributes["visible"].Equals("true", StringComparison.OrdinalIgnoreCase);
                                        replacement.Attributes.Remove("visible");
                                    }
                                    //Turn off ViewState
                                    replacement.EnableViewState = false;

                                    //Insert the new object next to the old, then remove the old.
                                    hgc.Parent.Controls.AddAt(hgc.Parent.Controls.IndexOf(hgc), replacement);
                                    hgc.Parent.Controls.Remove(hgc);
                                }
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
                                    hc = new fbs.PageBase.ScriptReference();
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
            //Always skips 'c'. This is so this function can be used on a TemplateControl directly. 
            //Skipping c doesn't have other side effects, because (p=c when c is ContentPlaceHolder)
            //later in code.
            Control p = c.Parent;
            if (p == null) p = c; //Unless there isn't a parent.

            //Find the first ContentPlaceHolder or TemplateControl in the ancestry 
            //(also stops at the root control)
            while ((p.Parent != null) && !(p is ContentPlaceHolder) && !(p is TemplateControl)) 
                p = p.Parent;

            //So this method can find the right value for a ContentPlaceHolder itself.
            //To make things work right, CPHs should act as members of the content page, 
            //since the CONTENT tags are replaced by them.
            if (c is ContentPlaceHolder) p = c;

            //If there aren't any CPHs in the immediate heirarchy, we have nothing to adjust for
            //(An intermediate TemplateControl (.ascx file, UserControl) makes it safe, since it
            //overrides the TemplateControl, etc. properties)
            if (!(p is ContentPlaceHolder)) return c.TemplateControl;

            //First, let's see if the bug is even happening. 
            //A child of a ContentPlaceHolder should never have the same TemplateControl value.
            if (p.TemplateControl != c.TemplateControl)
            {
                return c.TemplateControl; //Hey, it's different - maybe an intermediate PlaceHolder is
                //cleaning things up for us.
            }
            else if (p.AppRelativeTemplateSourceDirectory != c.AppRelativeTemplateSourceDirectory)
            {
                return c.TemplateControl; //Hey, it's been changed - must have already been corrected.
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
}

    