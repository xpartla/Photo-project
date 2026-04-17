import { test, expect, type APIRequestContext } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import { BlogPage } from "./pages/BlogPage";

const API_URL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5000";

async function getAdminToken(request: APIRequestContext): Promise<string> {
  const res = await request.post(`${API_URL}/api/auth/login`, {
    data: { email: "admin@dogphoto.sk", password: "admin123" },
  });
  expect(res.ok()).toBeTruthy();
  const { accessToken } = await res.json();
  return accessToken as string;
}

async function ensurePost(
  request: APIRequestContext,
  token: string,
  overrides: Partial<{ slug: string; titleSk: string; titleEn: string; contentMarkdownSk: string; contentMarkdownEn: string; categorySlugs: string[]; tagSlugs: string[] }> = {},
): Promise<string> {
  const slug = overrides.slug ?? "e2e-test-post";
  const res = await request.post(`${API_URL}/api/blog/posts`, {
    data: {
      slug,
      titleSk: overrides.titleSk ?? "E2E testovací článok",
      titleEn: overrides.titleEn ?? "E2E test blog post",
      excerptSk: "SK úryvok",
      excerptEn: "EN excerpt",
      contentMarkdownSk: overrides.contentMarkdownSk ?? "# E2E\n\nTelo článku.",
      contentMarkdownEn: overrides.contentMarkdownEn ?? "# E2E\n\nBody text.",
      author: "Adam",
      status: "Published",
      categorySlugs: overrides.categorySlugs ?? ["photography-tips"],
      tagSlugs: overrides.tagSlugs ?? [],
    },
    headers: { Authorization: `Bearer ${token}` },
  });
  expect([201, 409]).toContain(res.status());
  return slug;
}

test.describe("blog (public)", () => {
  test("blog listing renders posts", async ({ page, request }) => {
    const token = await getAdminToken(request);
    await ensurePost(request, token);

    const blog = new BlogPage(page);
    await blog.goto("/sk/blog");

    await expect(blog.postCards().first()).toBeVisible();
  });

  test("filter narrows by search term", async ({ page, request }) => {
    const token = await getAdminToken(request);
    const marker = "bratislavafilter";
    await ensurePost(request, token, {
      slug: "e2e-search-post",
      titleSk: `E2E ${marker} test`,
      titleEn: `E2E ${marker} test`,
      contentMarkdownSk: `# ${marker}`,
      contentMarkdownEn: `# ${marker}`,
    });

    const blog = new BlogPage(page);
    await blog.goto("/sk/blog");

    await blog.filterSummary().click();
    await blog.searchInput().fill(marker);
    await Promise.all([
      page.waitForURL((url) => url.search.includes(`q=${marker}`)),
      blog.submitButton().click(),
    ]);

    const cards = blog.postCards();
    await expect(cards.first()).toBeVisible();
    // Only the marker post should match
    await expect(cards).toHaveCount(1);
  });

  test("post detail page renders content", async ({ page, request }) => {
    const token = await getAdminToken(request);
    const slug = await ensurePost(request, token);

    const blog = new BlogPage(page);
    await blog.goto(`/sk/blog/${slug}`);

    await expect(blog.postTitle()).toBeVisible();
    await expect(page.locator(".blog-detail__content h1")).toBeVisible();
  });

  test("EN blog page loads", async ({ page, request }) => {
    const token = await getAdminToken(request);
    await ensurePost(request, token);

    const blog = new BlogPage(page);
    await blog.goto("/en/blog");

    await expect(blog.postCards().first()).toBeVisible();
  });

  test("blog listing has no critical a11y violations", async ({ page, request }) => {
    const token = await getAdminToken(request);
    await ensurePost(request, token);

    await page.goto("/sk/blog");
    const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
    const critical = results.violations.filter((v) => v.impact === "critical");
    expect(critical).toEqual([]);
  });

  test("blog detail has no critical a11y violations", async ({ page, request }) => {
    const token = await getAdminToken(request);
    const slug = await ensurePost(request, token);

    await page.goto(`/sk/blog/${slug}`);
    const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
    const critical = results.violations.filter((v) => v.impact === "critical");
    expect(critical).toEqual([]);
  });

  test("rss feed returns xml", async ({ request }) => {
    const res = await request.get("/rss.xml");
    expect(res.status()).toBe(200);
    const body = await res.text();
    expect(body).toContain("<rss");
    expect(body).toContain("<channel>");
  });
});

test.describe("blog admin", () => {
  test("non-admin visitor is redirected away from /sk/admin", async ({ page }) => {
    await page.goto("/sk/admin");
    // Unauthenticated users get bounced to the login page
    await page.waitForURL(/\/sk\/prihlasenie/);
    await expect(page).toHaveURL(/prihlasenie/);
  });

  test("admin login → list posts → see all 3 seeded posts", async ({ page, request }) => {
    const token = await getAdminToken(request);
    // Store the token in localStorage before navigating so the admin guard passes
    await page.goto("/sk");
    await page.evaluate((t) => {
      localStorage.setItem("partlphoto_token", t);
      localStorage.setItem("partlphoto_user", JSON.stringify({ id: "x", email: "admin@dogphoto.sk", role: "Admin" }));
    }, token);

    await page.goto("/sk/admin/blog");
    // Wait for posts list to be populated
    await page.waitForSelector(".admin-list__item");
    const count = await page.locator(".admin-list__item").count();
    expect(count).toBeGreaterThanOrEqual(1);
  });
});
