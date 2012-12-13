<%@ Page Language="C#" MasterPageFile="../master2/Master2.Master" EnableViewStateMac="false" AutoEventWireup="true" 
CodeFile="Page1.aspx.cs" Inherits="CPHFixes.Test.page.Page" Title="CPH bugs acid test" %>
<asp:Content ID="Content1" ContentPlaceHolderID="Head2" runat="server">

    <!-- These have visible='false', and should never be seen-->
    <link type="text/css" visible="false" rel="stylesheet" href="page1-hidden.css" />
    <link type="text/css" visible="false" rel="stylesheet" href="page1-hidden.css" runat="server" />
    
    <!-- Thes will only be loaded if the patch is running -->
    <link type="text/css" rel="stylesheet" href="page1.css" />
    <link type="text/css" rel="stylesheet" href="page1-server.css" runat="server" />
   
       <!-- The parsing of these doesn't affect output, just the intermediate control tree. We'll check these server-side and
         convert them to css selectors so we can have output -->
    <meta name="page1" content="css" />
    <meta name="page1-server" content="css" runat="server"/>
   
   <script type="text/javascript" src="script.js" ></script>
    
    
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="Body2" runat="server">

<h1>From page/page1.aspx (should 'pretend' to work if the url is page1/page.master, since no path resolution is needed).</h1>
        <div class="page1-hidden-showthis error" style="display:none;">
            page1-hidden.css loaded - it shouldn't have been rendered. The visible="false" attribute did not correctly hide one of the 'hidden' stylesheet references.
        </div>
        <div class="page1-hidden-hidethis ok">
            page1-hidden.css was not loaded. 
            If the other files failed to load, then this is just another failure.
            If the subsequent files loaded, then the visible="false" attribute correctly hid both of the 'hidden' stylesheet references.
        </div>
        
        <div class="page1-showthis ok" style="display:none;">
            page1.css loaded. This is inside a CPH, and isn't parsed by default. The patch should make this work, though. 
            If page/page1.aspx is accessed directly instead of through Test.aspx, then this will 'pretend' to work, simply
            because the browser can find the file anyway.
        </div>
        <div class="page1-hidethis error">
            page1.css failed. This is inside a CPH, and doesn't work by default. 
            This reference actually has to fight 2 bugs. 1) it isn't parsed, and 2) If it was parsed, the TemplateControl reference of the parent would be wrong.
            The patch should fix both issues.
        </div>
        <div class="page1-server-showthis ok" style="display:none;">
            page1-server.css loaded. (runat="server" reference) This is inside a CPH, and isn't parsed by default. The patch should make this work, though.
            If page/page1.aspx is accessed directly instead of through Test.aspx, then this will 'pretend' to work, simply
            because the browser can find the file anyway. Since the tag has runat="server" specified, it will be parsed into an HtmlGenericControl, but rendered back out
            without any path rebasing.
        </div>
        <div class="page1-server-hidethis error">
            page1-server.css failed. (runat="server" reference)  This is inside a CPH, and doesn't work by default. 
            This reference actually has to fight 2 bugs. 1) it isn't parsed, and 2) If it was parsed, the TemplateControl reference of the parent would be wrong.
            The patch should fix both issues.
        </div>
        
                    <div class="meta-page1-showthis ok" style="display:none;">
      meta name="page1" was found in the control tree. We're inside a CPH, so the patch is needed to make this work.    
    </div>
    
    <div class="meta-page1-hidethis error">
    meta name="page1" was not found in the control tree. We're inside a CPH, so the patch is needed to make this work.  
    </div>
    
        <div class="meta-page1-server-showthis ok" style="display:none;">
      meta name="page1-server" was found in the control tree.We're inside a CPH, so the patch is needed to make this work.   
    </div>
    
    <div class="meta-page1-server-hidethis error">
    meta name="page1-server" was not found in the control tree. We're inside a CPH, so the patch is needed to make this work.    
    </div>
                <div id="div1" runat="server">
        The Parent.TemplateControl.AppRelativeVirtualPath property here is <asp:Label ID="lbl1" runat="server" />.
        Unless the framework has been fixed, this should be pointing the the previous template control. You can use
        GetAdjustedParentTemplateControl(Parent) instead of Parent.TemplateControl to avoid this problem.
        </div>

        <div id="div2" runat="server">
        <asp:PlaceHolder runat="server">
        &lt;asp:PlaceHolder runat="server">
        <p>
        The Parent.TemplateControl.AppRelativeVirtualPath property here is <asp:Label ID="lbl2" runat="server" />.
        This should be always work, since the intermedate asp:PlaceHolder control will have cached it's TemplateControl property from before the insertion of the master page controls.</p>
        
        </p>
        &lt;/asp:PlaceHolder>
        </asp:PlaceHolder>
        </div>
                        <div id="div3" runat="server">
        The GetAdjustedParentTemplateControl(Parent).AppRelativeVirtualPath 
        property here is <asp:Label ID="lbl3" runat="server" />.
        </div>
        <script type="text/javascript">
        //<!--
         var m = " Script references don't work by default, but the patch should parse this into a ScriptReference instance.";
          if (window.page1) ok("Loaded page/script.js. Fixed with patch. Will appear to work if page/Page1.aspx is accessed directly, since the browser can resolve it without help.");
          else err("Failed to load page/script.js." + m);

        //-->
        </script>

</asp:Content>
