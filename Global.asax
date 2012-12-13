<%@ Application Language="C#" %>

<script runat="server">

    protected void Application_BeginRequest(object sender, EventArgs e) {
        //URL Rewrite / to /page/Page1.aspx
        if (System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath.TrimEnd('/').Equals(HttpContext.Current.Request.Path.TrimEnd('/'))) {
            HttpContext.Current.RewritePath(System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath.TrimEnd('/') + "/page/Page1.aspx", null,null,false);
        }
    }

    
       
</script>
