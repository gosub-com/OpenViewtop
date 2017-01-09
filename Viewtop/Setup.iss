[Setup]
AppName=Open Viewtop
AppVersion=0.0.5
OutputDir=.
OutputBaseFilename=SetupOpenViewtop-0.0.5
UsePreviousAppDir=false
UsePreviousGroup=false
DefaultDirName={pf}\Gosub\Open Viewtop
DefaultGroupName=Open Viewtop
AppPublisher=Gosub Software
UninstallDisplayName=Open Viewtop
UninstallDisplayIcon={app}\Gosub.Viewtop.exe
LicenseFile=License.txt

[Files]
Source: "Gosub.Viewtop.exe"; DestDir: "{app}"; flags:ignoreversion
Source: "www\*"; DestDir: "{app}\www"; flags:recursesubdirs ignoreversion
Source: "Newtonsoft.Json.dll"; DestDir: "{app}"; flags:ignoreversion
Source: "Mono.Security.dll"; DestDir: "{app}"; flags:ignoreversion

[Icons]
Name: "{group}\Open Viewtop"; Filename: "{app}\Gosub.Viewtop.exe"
Name: "{group}\Uninstall Open Viewtop"; Filename: "{uninstallexe}"


[Code]
// **************************************************************************
// The following code is for adding rules to the firewall
// **************************************************************************

// http://www.vincenzo.net/isxkb/index.php?title=Adding_a_rule_to_the_Windows_firewall
// http://stackoverflow.com/questions/5641839/programmatically-add-an-application-to-all-profile-windows-firewall-vista
// Utility functions for Inno Setup used to add/remove programs from the windows firewall rules
// Code originally from http://news.jrsoftware.org/news/innosetup/msg43799.html

const
  PORT_HTTPS = 24707;
  PORT_HTTP = 24708;
  PORT_HTTPS_ALT = 24709;
  PORT_HTTP_ALT = 24710;

  NET_FW_SCOPE_ALL = 0;
  NET_FW_IP_VERSION_ANY = 2;
  NET_FW_ACTION_ALLOW = 1;
  NET_FW_PROTOCOL_TCP = 6;
  NET_FW_PROTOCOL_UDP = 17;


procedure SetFirewallExceptionXP(AppName,FileName:string);
var
  FirewallObject: Variant;
  FirewallManager: Variant;
  FirewallProfile: Variant;
begin
  try
    FirewallObject := CreateOleObject('HNetCfg.FwAuthorizedApplication');
    FirewallObject.ProcessImageFileName := FileName;
    FirewallObject.Name := AppName;
    FirewallObject.Scope := NET_FW_SCOPE_ALL;
    FirewallObject.IpVersion := NET_FW_IP_VERSION_ANY;
    FirewallObject.Enabled := True;
    FirewallManager := CreateOleObject('HNetCfg.FwMgr');
    FirewallProfile := FirewallManager.LocalPolicy.CurrentProfile;
    FirewallProfile.AuthorizedApplications.Add(FirewallObject);
  except
  end;
end;

procedure SetFirewallExceptionVista(AppName,FileName:string);
var
  firewallRule: Variant;
  firewallPolicy: Variant;
begin
  try
    firewallRule := CreateOleObject('HNetCfg.FWRule');
    firewallRule.Action := NET_FW_ACTION_ALLOW;
    firewallRule.Description := AppName;
    firewallRule.ApplicationName := FileName;
    firewallRule.Enabled := True;
    firewallRule.InterfaceTypes := 'All';
    firewallRule.Name := AppName;

    firewallPolicy := CreateOleObject('HNetCfg.FwPolicy2');
    firewallPolicy.Rules.Add(firewallRule);
  except
  end;
end;

procedure SetFirewallException(AppName,FileName:string);
var
  WindVer: TWindowsVersion;
begin
  try
    GetWindowsVersionEx(WindVer);
    if WindVer.NTPlatform and (WindVer.Major >= 6) then
      SetFirewallExceptionVista(AppName,FileName)
    else
      SetFirewallExceptionXP(AppName,FileName);
  except
  end;
end;

procedure RemoveFirewallException( FileName:string );
var
  FirewallManager: Variant;
  FirewallProfile: Variant;
begin
  try
    FirewallManager := CreateOleObject('HNetCfg.FwMgr');
    FirewallProfile := FirewallManager.LocalPolicy.CurrentProfile;
    FireWallProfile.AuthorizedApplications.Remove(FileName);
  except
  end;
end;


procedure SetFirewallPortException(AppName: string; Protocol, Port: integer);
var
  FirewallObject: Variant;
  FirewallManager: Variant;
  FirewallProfile: Variant;
begin
  try
    FirewallObject := CreateOleObject('HNetCfg.FwOpenPort');
    FirewallObject.Name := AppName;
    FirewallObject.Scope := NET_FW_SCOPE_ALL;
    FirewallObject.IpVersion := NET_FW_IP_VERSION_ANY;
    FirewallObject.Protocol := Protocol;
    FirewallObject.Port := Port;
    FirewallObject.Enabled := True;
    FirewallManager := CreateOleObject('HNetCfg.FwMgr');
    FirewallProfile := FirewallManager.LocalPolicy.CurrentProfile;
    FirewallProfile.GloballyOpenPorts.Add(FirewallObject);
  except
  end;
end;    

procedure RemoveFirewallPortException(Protocol, Port: integer);
var
  FirewallManager: Variant;
  FirewallProfile: Variant;
begin
  try
    FirewallManager := CreateOleObject('HNetCfg.FwMgr');
    FirewallProfile := FirewallManager.LocalPolicy.CurrentProfile;
    FireWallProfile.GloballyOpenPorts.Remove(Port, Protocol);
  except
  end;
end;

// **************************************************************************
// Add firewall rules, start and stop services
// **************************************************************************


procedure CurStepChanged(CurStep: TSetupStep);
begin
  // Add firewall rules
  if CurStep = ssPostInstall then begin
    // Allow direct communications to Viewtop.exe (i.e. Beacon, UDP, TCP)
    SetFirewallException('Open Viewtop', ExpandConstant('{app}')+'\Gosub.Viewtop.exe');

	// NOTE: These are necessary because HTTP goes through a driver instead of direct to Viewtop.exe
    SetFirewallPortException('Open Viewport HTTPS', NET_FW_PROTOCOL_TCP, PORT_HTTPS);
    SetFirewallPortException('Open Viewport HTTP', NET_FW_PROTOCOL_TCP, PORT_HTTP);
    SetFirewallPortException('Open Viewport HTTPS ALT', NET_FW_PROTOCOL_TCP, PORT_HTTPS_ALT);
    SetFirewallPortException('Open Viewport HTTP ALT', NET_FW_PROTOCOL_TCP, PORT_HTTP_ALT);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin  
  // Remove firewall rules after uninstalling
  if CurUninstallStep = usPostUninstall then begin
     RemoveFirewallException(ExpandConstant('{app}')+'\Gosub.Viewtop.exe');
     RemoveFirewallPortException(NET_FW_PROTOCOL_TCP, PORT_HTTPS);
     RemoveFirewallPortException(NET_FW_PROTOCOL_TCP, PORT_HTTP);
     RemoveFirewallPortException(NET_FW_PROTOCOL_TCP, PORT_HTTPS_ALT);
     RemoveFirewallPortException(NET_FW_PROTOCOL_TCP, PORT_HTTP_ALT);
  end;
end;
