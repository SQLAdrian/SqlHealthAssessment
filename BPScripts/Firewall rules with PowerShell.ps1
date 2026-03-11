New-NetFirewallRule -DisplayName "01. SQL Server" -Direction Inbound –Protocol TCP –LocalPort 1433 -Action allow
New-NetFirewallRule -DisplayName "02. SQL Admin Connection" -Direction Inbound –Protocol TCP –LocalPort 1434 -Action allow
New-NetFirewallRule -DisplayName "03. SQL Database Management" -Direction Inbound –Protocol UDP –LocalPort 1434 -Action allow
New-NetFirewallRule -DisplayName "04. SQL Service Broker" -Direction Inbound –Protocol TCP –LocalPort 4022 -Action allow
New-NetFirewallRule -DisplayName "05. SQL Debugger/RPC" -Direction Inbound –Protocol TCP –LocalPort 135 -Action allow
#Enabling SQL Analysis Ports
New-NetFirewallRule -DisplayName "06. SQL Browser" -Direction Inbound –Protocol TCP –LocalPort 2382 -Action allow
#Enabling Misc. Applications
New-NetFirewallRule -DisplayName "07. HTTP" -Direction Inbound –Protocol TCP –LocalPort 80 -Action allow
New-NetFirewallRule -DisplayName "08. SSL" -Direction Inbound –Protocol TCP –LocalPort 443 -Action allow
New-NetFirewallRule -DisplayName "09. SQL Server Browse Button Service" -Direction Inbound –Protocol UDP –LocalPort 1433 -Action allow

New-NetFirewallRule -DisplayName "10. Cluster Service UDP"  -Direction Inbound –Protocol UDP –LocalPort 3343 -Action allow
New-NetFirewallRule -DisplayName "11. Cluster Service TCP"  -Direction Inbound –Protocol TCP –LocalPort 3343 -Action allow
New-NetFirewallRule -DisplayName "12. Cluster Service RPC"  -Direction Inbound –Protocol TCP –LocalPort 135 -Action allow
New-NetFirewallRule -DisplayName "13. Cluster Admin"  -Direction Inbound –Protocol UDP –LocalPort 137 -Action allow
New-NetFirewallRule -DisplayName "14. Cluster Admin Computer Authentication - UDP"  -Direction Inbound –Protocol UDP –LocalPort 464 -Action allow
New-NetFirewallRule -DisplayName "15. Cluster Admin Computer Authentication - UDP"  -Direction Inbound –Protocol TCP –LocalPort 464 -Action allow
New-NetFirewallRule -DisplayName "16. Cluster Admin Trusts"  -Direction Inbound –Protocol TCP –LocalPort 123 -Action allow
New-NetFirewallRule -DisplayName "17. Netlogon Cluster Admin"  -Direction Inbound –Protocol TCP –LocalPort 137 -Action allow
New-NetFirewallRule -DisplayName "18. Netlogon 2 Cluster Admin"  -Direction Inbound –Protocol TCP –LocalPort 138 -Action allow
New-NetFirewallRule -DisplayName "19. SQL AQ"  -Direction Inbound –Protocol TCP –LocalPort 5022 -Action allow
New-NetFirewallRule -DisplayName "20. SQL AG"  -Direction Inbound –Protocol TCP –LocalPort 5024 -Action allow
New-NetFirewallRule -DisplayName "21. SQL AG 5023"  -Direction Inbound –Protocol TCP –LocalPort 5023 -Action allow