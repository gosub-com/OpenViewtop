# Copyright #

Copyright (C) 2018 by Jeremy Spiller

The source code in this repository is licenesed under the GPL 2.0.
The GPL license does not apply to the trade marks "Gosub Software", 
"Open Viewtop", or to the graphical icons included in the repository 
(e.g OpenViewtop.ico, etc.) which remain copyright by Jeremy Spiller.

# Open Viewtop #

Open Viewtop is a remote desktop viewing application, much like VNC, except that a browser can be used as the viewing application.

Visit http://gosub.com/OpenViewtop for the latest news and download.

# Security #

All communications can be encrypted with the full power of HTTPS.  By default, a self signed certificate is used to authenticate the server and the browser will complain that the connection is insecure.  In reality, the connection is protected from passive monitoring and cannot be decrypted unless there is an active man in the middle attack (i.e. someone has to actively intercept and modify the communications link)

To protect against a man in the middle attack (and keep the browser from complaining), you can use a wild card domain SSL certificate (e.g. *.gosub.com), and point your sub-do