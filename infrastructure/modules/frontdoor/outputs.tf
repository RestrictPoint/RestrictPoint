output "profile_id" {
  description = "Front Door profile resource ID."
  value       = azurerm_cdn_frontdoor_profile.main.id
}

output "profile_name" {
  description = "Front Door profile name."
  value       = azurerm_cdn_frontdoor_profile.main.name
}

output "waf_policy_id" {
  description = "WAF policy resource ID."
  value       = var.enable_waf ? azurerm_cdn_frontdoor_firewall_policy.main[0].id : null
}

output "endpoint_hostname" {
  description = "Placeholder endpoint hostname (replace with actual endpoints when backends exist)."
  value       = azurerm_cdn_frontdoor_endpoint.placeholder.host_name
}
