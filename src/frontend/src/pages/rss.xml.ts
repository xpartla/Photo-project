import type { APIRoute } from "astro";

const API_BASE = import.meta.env.PUBLIC_API_URL || "http://localhost:5000";

export const GET: APIRoute = async () => {
  const res = await fetch(`${API_BASE}/rss.xml`);
  const body = await res.text();
  return new Response(body, {
    status: res.status,
    headers: {
      "Content-Type": "application/rss+xml; charset=utf-8",
      "Cache-Control": "public, max-age=300",
    },
  });
};
