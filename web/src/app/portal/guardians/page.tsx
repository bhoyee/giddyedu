"use client";

import { useState } from "react";
import { GuardianRegistrationForm } from "@/components/guardian-registration-form";
import { GuardianDirectory } from "@/components/guardian-directory";
import { ResourcePage } from "@/components/resource-page";

export default function GuardiansPage() {
  const [revision, setRevision] = useState(0);
  return <ResourcePage title="Guardians" description="Parent and guardian profiles linked to learners."
    endpoint="guardians" emptyMessage="No guardian profiles have been created." fields={[]} hideRecords
    managementSlot={<><GuardianRegistrationForm onCreated={() => setRevision(value => value + 1)} /><GuardianDirectory key={revision} /></>}/>;
}
