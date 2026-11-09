using System;
class ScopeCheck {
 static void Main() {
  if(SetupScope.AppFolder(false)==SetupScope.AppFolder(true))throw new Exception("Install scopes overlap");
  if(SetupScope.DesktopLink(false)==SetupScope.DesktopLink(true))throw new Exception("Desktop scopes overlap");
  if(SetupScope.MenuFolder(false)==SetupScope.MenuFolder(true))throw new Exception("Menu scopes overlap");
  if(SetupScope.StartupLink(false)==SetupScope.StartupLink(true))throw new Exception("Startup scopes overlap");
  Console.WriteLine("Separate install, desktop, Start menu and startup paths verified.");
 }
}
