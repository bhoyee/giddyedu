import { LegalPage, type LegalSection } from "@/components/legal-page";

const sections: LegalSection[] = [
  { title: "What cookies are", paragraphs: ["Cookies are small text records stored by a browser. They can keep a session secure, remember a choice or help a service understand how its public pages are used."] },
  { title: "Essential cookies", paragraphs: ["Essential cookies support security, authentication, session continuity, load balancing and saved privacy choices. GiddyEdu cannot provide these functions reliably without them, so they do not require optional consent."] },
  { title: "Analytics cookies", paragraphs: ["If enabled, analytics cookies may be used to understand aggregated website use, identify navigation problems and improve public journeys. They remain disabled unless a visitor accepts them."] },
  { title: "Marketing cookies", paragraphs: ["If enabled, marketing cookies may measure campaigns or help make communications more relevant. They remain disabled unless a visitor accepts them. GiddyEdu does not use school or student records for advertising."] },
  { title: "Managing your choice", paragraphs: ["Visitors can accept all optional cookies, reject them, or choose categories separately. The selection is remembered for up to one year and can be revisited through Cookie settings in the website footer. Browser controls can also remove stored cookies."] },
  { title: "Changes to cookie use", paragraphs: ["This policy and the consent controls will be updated when cookie categories or providers materially change. Optional technologies must be connected to the recorded consent before they are activated."] },
];

export default function CookiesPage() { return <LegalPage title="Cookie Policy" introduction="This policy explains which cookies GiddyEdu uses, why they are used and how visitors control optional categories." sections={sections} />; }
