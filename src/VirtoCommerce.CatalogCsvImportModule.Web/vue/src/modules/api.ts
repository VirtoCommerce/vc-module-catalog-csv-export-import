/** Small JSON fetch (same-origin cookie auth, like the generated clients). */
export async function http<T = unknown>(method: string, url: string, body?: unknown): Promise<T> {
  const res = await fetch(url, {
    method,
    headers: body !== undefined ? { "Content-Type": "application/json" } : {},
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  if (!res.ok) throw new Error(`${method} ${url}: ${res.status} ${await res.text()}`);
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

export const options = (values: string[]) => values.map((v) => ({ id: v, title: v }));

/** The CSV column delimiters (legacy columnDelimiters). */
export const delimiters = [",", ";", "|", "\t"].map((d) => ({ id: d, title: d === "\t" ? "Tab" : d }));
