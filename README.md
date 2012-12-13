ContentPlaceHolderTests
=======================

A set of tests from 2008 to demonstrate incorrect ContentPlaceHolder behavior.

This test app is kind of a 'acid test' for two bugs in the .net framework

1) Head tags such as <link> 


            //link, meta, script (with runat="server")
            //Visible, EnableViewState (default off)

            //parsing - verify client and server comments excluded
            //Visible, EnableViewState (default off)

            //Test parsing occurs
            // - in single master page
            // - in nested master page
            // - in second master page
            //Test visible attribute works
            //Test enableviewstate attribute works

            

            //Test correct path resolution when the browser url doesn't match the physical path
            //  -- and when the master page is in a different directory from the content page (the 2nd bug will rebase according to mp location)
            //  -- 
			