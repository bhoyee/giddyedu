import type { MetadataRoute } from "next";

export default function manifest(): MetadataRoute.Manifest {
  return {
    name: "GiddyEdu School Operating Platform",
    short_name: "GiddyEdu",
    description: "Secure school administration, academics, communication, and operations.",
    id: "/",
    start_url: "/",
    scope: "/",
    display: "standalone",
    background_color: "#f6f4ee",
    theme_color: "#12372a",
    orientation: "any",
    categories: ["education", "productivity"],
    icons: [
      { src: "/icons/giddyedu-192.png", sizes: "192x192", type: "image/png", purpose: "any" },
      { src: "/icons/giddyedu-512.png", sizes: "512x512", type: "image/png", purpose: "any" },
      { src: "/icons/giddyedu-maskable-512.png", sizes: "512x512", type: "image/png", purpose: "maskable" },
      { src: "/icons/giddyedu.svg", sizes: "any", type: "image/svg+xml", purpose: "any" },
    ],
  };
}
