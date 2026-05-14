import { basename } from "node:path";
import { PARTITION_INDEX_SENTINEL, REGION_CHUNK_AXIS } from "./region_layout.ts";
import { formatIssue } from "./region_issue.ts";
import { getMessages } from "./region_i18n.ts";
import type { Messages } from "./region_i18n.ts";
import type { RegionAnalysis, RegionMetrics } from "./region_types.ts";

function formatBytes(value: number): string {
  const units = ["B", "KiB", "MiB", "GiB"];
  let current = value;
  let unitIndex = 0;

  while (current >= 1024 && unitIndex < units.length - 1) {
    current /= 1024;
    unitIndex += 1;
  }

  return `${current.toFixed(current >= 10 || unitIndex === 0 ? 0 : 2)} ${units[unitIndex]}`;
}

function formatPercent(value: number): string {
  return `${(value * 100).toFixed(2)}%`;
}

function formatTimestamp(timestamp: number | null): string {
  if (timestamp === null) {
    return "-";
  }
  return new Date(timestamp).toISOString();
}

function safeRatio(numerator: number, denominator: number): number {
  if (denominator <= 0) {
    return 0;
  }
  return numerator / denominator;
}

function formatFraction(numerator: number, denominator: number): string {
  return `${numerator} / ${denominator} (${formatPercent(safeRatio(numerator, denominator))})`;
}

function bar(value: number, width = 18, ensureVisible = false): string {
  const clamped = Math.max(0, Math.min(1, value));
  const filled = clamped > 0 ? (ensureVisible ? Math.max(1, Math.round(clamped * width)) : Math.round(clamped * width)) : 0;
  return `${"█".repeat(filled)}${"░".repeat(Math.max(0, width - filled))}`;
}

function bullet(text: string, level = 0): string {
  return `${"  ".repeat(level)}- ${text}`;
}

function line(label: string, value: string, level = 0): string {
  return bullet(`${label}: ${value}`, level);
}

function metricLine(label: string, bytes: number, ratio?: number, level = 0): string {
  const detail =
    ratio === undefined
      ? `${formatBytes(bytes)}`
      : `${formatBytes(bytes)}  ${bar(ratio)}  ${formatPercent(ratio)}`;
  return line(label, detail, level);
}

function ratioLine(label: string, numerator: number, denominator: number, level = 0): string {
  const ratio = safeRatio(numerator, denominator);
  return line(label, `${numerator} / ${denominator}  ${bar(ratio, 18, true)}  ${formatPercent(ratio)}`, level);
}

function percentLine(label: string, ratio: number, level = 0): string {
  return line(label, `${bar(ratio)}  ${formatPercent(ratio)}`, level);
}

function totalCapacityText(metrics: RegionMetrics): string {
  return `${REGION_CHUNK_AXIS} × ${REGION_CHUNK_AXIS} = ${metrics.totalChunkCapacity}`;
}

function chainPreview(
  messages: Messages,
  indices: number[],
  maxChainPreview: number,
  terminatedWithSentinel: boolean,
  cyclic: boolean,
): string {
  if (indices.length === 0) {
    return "∅";
  }

  const preview = indices.slice(0, maxChainPreview).join("->");
  const suffix =
    indices.length > maxChainPreview
      ? "->..."
      : terminatedWithSentinel
        ? `--${messages.sentinelNoNext}:${PARTITION_INDEX_SENTINEL}`
        : "";
  const cycleSuffix = cyclic ? "->..." : "";
  return `${preview}${suffix}${cycleSuffix}`;
}

function distributionLines(messages: Messages, metrics: RegionMetrics): string[] {
  if (metrics.partitionCountDistribution.length === 0) {
    return [bullet(messages.distributionEmpty)];
  }

  return metrics.partitionCountDistribution.map((entry) =>
    bullet(
      `${messages.partitionBucketLabel(entry.partitionCount)}: ${entry.chunkCount}  ${bar(entry.ratioOfLoadedChunks)}  ${formatPercent(entry.ratioOfLoadedChunks)}`,
    ),
  );
}

function hint(text: string, level = 0): string {
  return `${"  ".repeat(level)}(${text})`;
}

function renderRegionSummary(messages: Messages, analysis: RegionAnalysis, maxChainPreview: number, lang: string): string[] {
  const { parse, metrics } = analysis;
  const lines: string[] = [];
  const unloadedChunkCount = Math.max(0, metrics.totalChunkCapacity - metrics.loadedChunkCount);

  lines.push(`${messages.appTitle}  ${basename(parse.path)}`);
  lines.push(line(messages.filePath, parse.path));
  lines.push(line(messages.createdAt, formatTimestamp(parse.createTimestamp)));
  lines.push(
    line(
      messages.status,
      `${messages.format}:${parse.formatOk ? messages.ok : messages.bad} | ` +
        `${messages.signature}:${parse.signatureOk ? messages.ok : messages.bad} | ` +
        `${messages.alignment}:${parse.alignmentOk ? messages.ok : messages.bad}`,
    ),
  );
  lines.push("");

  lines.push(messages.summary);
  lines.push(line(messages.totalChunkCapacity, totalCapacityText(metrics)));
  lines.push(line(messages.loadedChunks, `${metrics.loadedChunkCount}`, 1));
  lines.push(ratioLine(messages.loadedChunkCapacityRatio, metrics.loadedChunkCount, metrics.totalChunkCapacity, 1));
  lines.push(line(messages.unusedChunks, `${unloadedChunkCount}`, 1));
  lines.push(ratioLine(messages.unusedChunkRatio, unloadedChunkCount, metrics.totalChunkCapacity, 1));
  lines.push(metricLine(messages.totalOccupiedBytes, metrics.totalOccupiedBytes));
  lines.push(metricLine(messages.actualDataBytes, metrics.actualDataBytes, metrics.actualRatioOfTotal, 1));
  lines.push(metricLine(messages.totalInvalidBytes, metrics.totalInvalidBytes, metrics.invalidRatioOfTotal, 1));
  lines.push(line(messages.allocatedPartitionCount, `${metrics.allocatedPartitionCount}`));
  lines.push(line(messages.activePartitionCount, `${metrics.activePartitionCount}`));
  lines.push(hint(messages.activePartitionCountHint, 1));
  lines.push(line(messages.freePartitionCount, `${metrics.freePartitionCount}`));
  lines.push(hint(messages.freePartitionCountHint, 1));
  lines.push(line(messages.orphanPartitionCount, `${metrics.orphanPartitionCount}`));
  lines.push(hint(messages.orphanPartitionCountHint, 1));
  lines.push("");

  lines.push(messages.partitionDistribution);
  lines.push(...distributionLines(messages, metrics));
  lines.push("");

  lines.push(messages.layout);
  lines.push(bullet(messages.totalOccupiedBytes));
  lines.push(metricLine(messages.prefixInvalidBytes, metrics.prefixInvalidBytes, safeRatio(metrics.prefixInvalidBytes, metrics.totalOccupiedBytes), 1));
  lines.push(metricLine(messages.partitionAreaBytes, metrics.partitionAreaBytes, safeRatio(metrics.partitionAreaBytes, metrics.totalOccupiedBytes), 1));
  lines.push(percentLine(messages.partitionValidRatio, metrics.partitionValidRatio, 2));
  lines.push(metricLine(messages.partitionInvalidBytes, metrics.partitionInvalidBytes, metrics.partitionInvalidRatio, 2));
  lines.push(metricLine(messages.freePartitionBytes, metrics.freePartitionBytes, metrics.freeShareOfPartitionInvalid, 3));
  lines.push(
    metricLine(
      messages.writtenPartitionUnwrittenBytes,
      metrics.writtenPartitionUnwrittenBytes,
      metrics.unwrittenShareOfPartitionInvalid,
      3,
    ),
  );
  lines.push(
    metricLine(
      messages.activeNextPointerBytes,
      metrics.activeNextPointerBytes,
      safeRatio(metrics.activeNextPointerBytes, metrics.partitionInvalidBytes),
      3,
    ),
  );
  if (metrics.orphanPartitionBytes > 0) {
    lines.push(
      metricLine(
        messages.orphanPartitionBytes,
        metrics.orphanPartitionBytes,
        safeRatio(metrics.orphanPartitionBytes, metrics.partitionInvalidBytes),
        3,
      ),
    );
  }
  lines.push("");

  lines.push(messages.freeChain);
  lines.push(line(messages.freePartitionCount, `${parse.freeChain.indices.length}`));
  lines.push(
    line(
      messages.preview,
      chainPreview(messages, parse.freeChain.indices, maxChainPreview, parse.freeChain.terminatedWithSentinel, parse.freeChain.cyclic),
    ),
  );
  lines.push(
    line(
      messages.headLabel,
      parse.freeChain.state.headFreePartitionIndex === PARTITION_INDEX_SENTINEL
        ? "∅"
        : `${parse.freeChain.state.headFreePartitionIndex}`,
    ),
  );
  lines.push("");

  lines.push(messages.findings);
  if (parse.findings.length === 0) {
    lines.push(`- ✅ ${messages.healthy}`);
  } else {
    for (const finding of parse.findings) {
      lines.push(`- ⚠️ ${formatIssue(finding, lang)}`);
    }
  }

  return lines;
}

function sumMetrics(analyses: RegionAnalysis[]): RegionMetrics {
  const distributionMap = new Map<number, number>();
  const totals = analyses.reduce(
    (accumulator, analysis) => {
      const { metrics } = analysis;
      accumulator.loadedChunkCount += metrics.loadedChunkCount;
      accumulator.totalChunkCapacity += metrics.totalChunkCapacity;
      accumulator.loadedChunkCapacityRatio += metrics.loadedChunkCapacityRatio;
      accumulator.declaredNonEmptyChunkCount += metrics.declaredNonEmptyChunkCount;
      accumulator.totalOccupiedBytes += metrics.totalOccupiedBytes;
      accumulator.partitionAreaBytes += metrics.partitionAreaBytes;
      accumulator.actualDataBytes += metrics.actualDataBytes;
      accumulator.totalInvalidBytes += metrics.totalInvalidBytes;
      accumulator.prefixInvalidBytes += metrics.prefixInvalidBytes;
      accumulator.partitionInvalidBytes += metrics.partitionInvalidBytes;
      accumulator.writtenPartitionUnwrittenBytes += metrics.writtenPartitionUnwrittenBytes;
      accumulator.freePartitionBytes += metrics.freePartitionBytes;
      accumulator.activeNextPointerBytes += metrics.activeNextPointerBytes;
      accumulator.orphanPartitionBytes += metrics.orphanPartitionBytes;
      accumulator.allocatedPartitionCount += metrics.allocatedPartitionCount;
      accumulator.activePartitionCount += metrics.activePartitionCount;
      accumulator.freePartitionCount += metrics.freePartitionCount;
      accumulator.orphanPartitionCount += metrics.orphanPartitionCount;

      for (const entry of metrics.partitionCountDistribution) {
        distributionMap.set(entry.partitionCount, (distributionMap.get(entry.partitionCount) ?? 0) + entry.chunkCount);
      }
      return accumulator;
    },
    {
      loadedChunkCount: 0,
      totalChunkCapacity: 0,
      loadedChunkCapacityRatio: 0,
      declaredNonEmptyChunkCount: 0,
      totalOccupiedBytes: 0,
      partitionAreaBytes: 0,
      actualDataBytes: 0,
      totalInvalidBytes: 0,
      prefixInvalidBytes: 0,
      partitionInvalidBytes: 0,
      writtenPartitionUnwrittenBytes: 0,
      freePartitionBytes: 0,
      activeNextPointerBytes: 0,
      orphanPartitionBytes: 0,
      allocatedPartitionCount: 0,
      activePartitionCount: 0,
      freePartitionCount: 0,
      orphanPartitionCount: 0,
      invalidRatioOfTotal: 0,
      actualRatioOfTotal: 0,
      partitionInvalidRatio: 0,
      partitionValidRatio: 0,
      freeShareOfPartitionInvalid: 0,
      unwrittenShareOfPartitionInvalid: 0,
      partitionCountDistribution: [],
    },
  );

  return {
    ...totals,
    loadedChunkCapacityRatio: safeRatio(totals.loadedChunkCount, totals.totalChunkCapacity),
    invalidRatioOfTotal: safeRatio(totals.totalInvalidBytes, totals.totalOccupiedBytes),
    actualRatioOfTotal: safeRatio(totals.actualDataBytes, totals.totalOccupiedBytes),
    partitionInvalidRatio: safeRatio(totals.partitionInvalidBytes, totals.partitionAreaBytes),
    partitionValidRatio: safeRatio(totals.actualDataBytes, totals.partitionAreaBytes),
    freeShareOfPartitionInvalid: safeRatio(totals.freePartitionBytes, totals.partitionInvalidBytes),
    unwrittenShareOfPartitionInvalid: safeRatio(totals.writtenPartitionUnwrittenBytes, totals.partitionInvalidBytes),
    partitionCountDistribution: [...distributionMap.entries()]
      .sort((left, right) => left[0] - right[0])
      .map(([partitionCount, chunkCount]) => ({
        partitionCount,
        chunkCount,
        ratioOfLoadedChunks: safeRatio(chunkCount, totals.loadedChunkCount),
      })),
  };
}

function renderAggregate(messages: Messages, analyses: RegionAnalysis[]): string[] {
  const metrics = sumMetrics(analyses);
  const unloadedChunkCount = Math.max(0, metrics.totalChunkCapacity - metrics.loadedChunkCount);
  const lines = [
    messages.totals,
    `- ${messages.aggregate}: ${analyses.length}`,
    line(messages.totalChunkCapacity, totalCapacityText(metrics)),
    line(messages.loadedChunks, `${metrics.loadedChunkCount}`, 1),
    ratioLine(messages.loadedChunkCapacityRatio, metrics.loadedChunkCount, metrics.totalChunkCapacity, 1),
    line(messages.unusedChunks, `${unloadedChunkCount}`, 1),
    ratioLine(messages.unusedChunkRatio, unloadedChunkCount, metrics.totalChunkCapacity, 1),
    metricLine(messages.totalOccupiedBytes, metrics.totalOccupiedBytes),
    metricLine(messages.actualDataBytes, metrics.actualDataBytes, metrics.actualRatioOfTotal, 1),
    metricLine(messages.totalInvalidBytes, metrics.totalInvalidBytes, metrics.invalidRatioOfTotal, 1),
    line(messages.allocatedPartitionCount, `${metrics.allocatedPartitionCount}`),
    line(messages.activePartitionCount, `${metrics.activePartitionCount}`),
    hint(messages.activePartitionCountHint, 1),
    line(messages.freePartitionCount, `${metrics.freePartitionCount}`),
    hint(messages.freePartitionCountHint, 1),
    line(messages.orphanPartitionCount, `${metrics.orphanPartitionCount}`),
    hint(messages.orphanPartitionCountHint, 1),
  ];

  lines.push("");
  lines.push(messages.partitionDistribution);
  lines.push(...distributionLines(messages, metrics));

  return lines;
}

export function renderHumanReadable(analyses: RegionAnalysis[], lang: string, maxChainPreview: number): string {
  const messages = getMessages(lang);
  if (analyses.length === 0) {
    return messages.noFiles;
  }

  const sections = analyses.map((analysis) => renderRegionSummary(messages, analysis, maxChainPreview, lang).join("\n"));
  if (analyses.length > 1) {
    sections.push(renderAggregate(messages, analyses).join("\n"));
  }
  return sections.join("\n\n");
}

export function renderFreeChainOnly(analyses: RegionAnalysis[], lang: string, maxChainPreview: number): string {
  const messages = getMessages(lang);
  if (analyses.length === 0) {
    return messages.noFiles;
  }

  return analyses
    .map(({ parse, metrics }) => {
      const findings =
        parse.freeChain.errors.length === 0
          ? [`- ✅ ${messages.healthy}`]
          : parse.freeChain.errors.map((error) => `- ⚠️ ${formatIssue(error, lang)}`);

      return [
        `${messages.freeChain}  ${basename(parse.path)}`,
        line(messages.filePath, parse.path),
        line(messages.freePartitionCount, `${metrics.freePartitionCount}`),
        line(messages.bytesLabel, formatBytes(metrics.freePartitionBytes)),
        line(
          messages.preview,
          chainPreview(messages, parse.freeChain.indices, maxChainPreview, parse.freeChain.terminatedWithSentinel, parse.freeChain.cyclic),
        ),
        ...findings,
      ].join("\n");
    })
    .join("\n\n");
}
