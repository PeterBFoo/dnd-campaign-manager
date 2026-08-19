output "instance_id" {
  description = "OCID de la instancia de aplicación."
  value       = oci_core_instance.application.id
}

output "public_ip" {
  description = "IPv4 pública de la instancia."
  value       = oci_core_instance.application.public_ip
}

output "ssh_command" {
  description = "Comando orientativo de conexión."
  value       = "ssh ubuntu@${oci_core_instance.application.public_ip}"
}

output "deployment_root" {
  description = "Directorio persistente de despliegue del host."
  value       = "/opt/dnd-campaign-manager"
}
