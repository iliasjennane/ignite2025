import https from "https";
import { NextResponse } from "next/server";

// Proxy Copilot checklist requests to the .NET AG UI backend while normalizing the payload.
const apiUrl = process.env.NEXT_PUBLIC_API_URL;
const insecureHttpsAgent = new https.Agent({ rejectUnauthorized: false });

const DEFAULT_AGENT = "chief_analyst";

export async function POST(req: Request) {
  if (!apiUrl) {
    return NextResponse.json(
      { error: "NEXT_PUBLIC_API_URL is not configured" },
      { status: 500 }
    );
  }

  let body: { focus?: string; timeframe?: string; agentId?: string } = {};
  try {
    body = await req.json();
  } catch {
    return NextResponse.json(
      { error: "Invalid JSON payload" },
      { status: 400 }
    );
  }

  const { focus, timeframe, agentId } = body;
  const targetAgent = (typeof agentId === "string" && agentId.length > 0) ? agentId : DEFAULT_AGENT;

  const promptSegments = [
    "Create a structured execution checklist with 4-6 tightly scoped actions.",
    "Each action must include a short label, the suggested owner, a due horizon, and a one-line success indicator.",
    "Return concise copy suitable for an operations stand-up.",
  ];

  if (focus && focus.trim().length > 0) {
    promptSegments.push(`Focus specifically on: ${focus.trim()}.`);
  }

  if (timeframe && timeframe.trim().length > 0) {
    promptSegments.push(`Align the plan to the following timeframe: ${timeframe.trim()}.`);
  }

  const outbound = {
    agent: targetAgent,
    messages: [
      {
        role: "user",
        content: promptSegments.join(" \n"),
      },
    ],
    stream: false,
  };

  try {
    const response = await fetch(`${apiUrl}/api/agui/chat`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(outbound),
      // @ts-expect-error - Next.js fetch typing omits Node HTTPS agents
      agent: apiUrl.startsWith("https") ? insecureHttpsAgent : undefined,
    });

    if (!response.ok) {
      const errorText = await response.text();
      return NextResponse.json(
        { error: `AG UI backend error ${response.status}: ${errorText}` },
        { status: 502 }
      );
    }

    const data = await response.json();
    const content = typeof data?.content === "string" ? data.content : JSON.stringify(data ?? {});
    const normalized = normalizeChecklist(content);

    return NextResponse.json(normalized);
  } catch (error: unknown) {
    const message = error instanceof Error ? error.message : "Failed to contact AG UI backend";
    return NextResponse.json({ error: message }, { status: 500 });
  }
}

function normalizeChecklist(raw: string) {
  const lines = raw.split(/\r?\n/);
  const items: Array<{ label: string; owner?: string; due?: string; status?: string }> = [];
  const summaryParts: string[] = [];

  lines.forEach((line) => {
    const trimmed = line.trim();
    if (!trimmed) {
      return;
    }

    const isBullet = /^[-*•\d]+[.)]?\s+/.test(trimmed);
    if (!isBullet && summaryParts.length === 0) {
      summaryParts.push(trimmed);
      return;
    }

    if (!isBullet) {
      return;
    }

    const withoutBullet = trimmed.replace(/^[-*•\d]+[.)]?\s+/, "");
    const owner = extractField(withoutBullet, /owner\s*[:|-]\s*([^;|,]+)/i);
    const due = extractField(withoutBullet, /due\s*[:|-]\s*([^;|,]+)/i);
    const status = extractField(withoutBullet, /status\s*[:|-]\s*([^;|,]+)/i);

    const cleanedLabel = withoutBullet
      .replace(/owner\s*[:|-].*/i, "")
      .replace(/due\s*[:|-].*/i, "")
      .replace(/status\s*[:|-].*/i, "")
      .trim()
      .replace(/[;|,]$/g, "")
      .trim();

    items.push({
      label: cleanedLabel || withoutBullet,
      owner: owner || undefined,
      due: due || undefined,
      status: status || undefined,
    });
  });

  return {
    title: "Execution Checklist",
    summary: summaryParts.join(" ") || undefined,
    items,
    raw,
  };
}

function extractField(source: string, regex: RegExp) {
  const match = source.match(regex);
  if (!match) {
    return undefined;
  }
  return match[1]?.trim();
}
