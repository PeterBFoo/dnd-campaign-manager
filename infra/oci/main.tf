data "oci_identity_availability_domains" "available" {
  compartment_id = var.tenancy_ocid
}

locals {
  availability_domain = var.availability_domain != "" ? var.availability_domain : data.oci_identity_availability_domains.available.availability_domains[0].name
  common_tags = {
    application = "dnd-campaign-manager"
    managed_by  = "terraform"
  }
}

data "oci_core_images" "ubuntu_arm" {
  compartment_id           = var.tenancy_ocid
  operating_system         = "Canonical Ubuntu"
  operating_system_version = "24.04"
  shape                    = "VM.Standard.A1.Flex"
  sort_by                  = "TIMECREATED"
  sort_order               = "DESC"
}

resource "oci_core_vcn" "application" {
  compartment_id = var.compartment_ocid
  cidr_blocks    = ["10.42.0.0/16"]
  display_name   = "dnd-campaign-vcn"
  dns_label      = "dndcampaign"
  freeform_tags  = local.common_tags
}

resource "oci_core_internet_gateway" "application" {
  compartment_id = var.compartment_ocid
  vcn_id         = oci_core_vcn.application.id
  display_name   = "dnd-campaign-internet-gateway"
  enabled        = true
  freeform_tags  = local.common_tags
}

resource "oci_core_route_table" "public" {
  compartment_id = var.compartment_ocid
  vcn_id         = oci_core_vcn.application.id
  display_name   = "dnd-campaign-public-routes"
  freeform_tags  = local.common_tags

  route_rules {
    destination       = "0.0.0.0/0"
    destination_type  = "CIDR_BLOCK"
    network_entity_id = oci_core_internet_gateway.application.id
  }
}

resource "oci_core_security_list" "application" {
  compartment_id = var.compartment_ocid
  vcn_id         = oci_core_vcn.application.id
  display_name   = "dnd-campaign-security-list"
  freeform_tags  = local.common_tags

  egress_security_rules {
    destination = "0.0.0.0/0"
    protocol    = "all"
  }

  ingress_security_rules {
    source   = var.ssh_allowed_cidr
    protocol = "6"
    tcp_options {
      min = 22
      max = 22
    }
  }

  ingress_security_rules {
    source   = "0.0.0.0/0"
    protocol = "6"
    tcp_options {
      min = 80
      max = 80
    }
  }

  ingress_security_rules {
    source   = "0.0.0.0/0"
    protocol = "6"
    tcp_options {
      min = 443
      max = 443
    }
  }

  ingress_security_rules {
    source   = "0.0.0.0/0"
    protocol = "17"
    udp_options {
      min = 443
      max = 443
    }
  }
}

resource "oci_core_subnet" "public" {
  compartment_id             = var.compartment_ocid
  vcn_id                     = oci_core_vcn.application.id
  cidr_block                 = "10.42.1.0/24"
  display_name               = "dnd-campaign-public-subnet"
  dns_label                  = "public"
  route_table_id             = oci_core_route_table.public.id
  security_list_ids          = [oci_core_security_list.application.id]
  prohibit_public_ip_on_vnic = false
  prohibit_internet_ingress  = false
  freeform_tags              = local.common_tags
}

resource "oci_core_instance" "application" {
  availability_domain = local.availability_domain
  compartment_id      = var.compartment_ocid
  display_name        = "dnd-campaign-manager"
  shape               = "VM.Standard.A1.Flex"
  freeform_tags       = local.common_tags

  shape_config {
    ocpus         = var.instance_ocpus
    memory_in_gbs = var.instance_memory_gbs
  }

  create_vnic_details {
    assign_public_ip = true
    display_name     = "dnd-campaign-primary-vnic"
    hostname_label   = "app"
    subnet_id        = oci_core_subnet.public.id
  }

  metadata = {
    ssh_authorized_keys = var.ssh_public_key
    user_data = base64encode(templatefile("${path.module}/cloud-init.yaml.tftpl", {
      deployment_root = "/opt/dnd-campaign-manager"
    }))
  }

  source_details {
    source_type             = "image"
    source_id               = data.oci_core_images.ubuntu_arm.images[0].id
    boot_volume_size_in_gbs = var.boot_volume_size_gbs
  }

  lifecycle {
    precondition {
      condition     = length(data.oci_core_images.ubuntu_arm.images) > 0
      error_message = "No se encontró una imagen Ubuntu 24.04 compatible con Ampere A1 en la región."
    }
  }
}
