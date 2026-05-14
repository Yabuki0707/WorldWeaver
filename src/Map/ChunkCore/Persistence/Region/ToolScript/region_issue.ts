import { getMessages } from "./region_i18n.ts";
import type { LanguageCode, Messages } from "./region_i18n.ts";
import type { RegionIssue } from "./region_types.ts";

function formatChunkScope(messages: Messages, issue: RegionIssue): string {
  if (!issue.scope || issue.scope.kind !== "chunk") {
    return "";
  }

  return `${messages.chunkScope(issue.scope.localX, issue.scope.localY)}: `;
}

function formatCoreIssue(messages: Messages, issue: RegionIssue): string {
  const params = issue.params ?? {};

  switch (issue.code) {
    case "format_area_empty":
      return messages.issueFormatAreaEmpty;
    case "format_json_parse_failed":
      return messages.issueFormatJsonParseFailed(String(params.reason ?? "-"));
    case "format_type_mismatch":
      return messages.issueFormatTypeMismatch(String(params.path ?? "-"));
    case "format_array_length_mismatch":
      return messages.issueFormatArrayLengthMismatch(String(params.path ?? "-"));
    case "format_missing_field":
      return messages.issueFormatMissingField(String(params.path ?? "-"));
    case "format_value_mismatch":
      return messages.issueFormatValueMismatch(String(params.path ?? "-"));
    case "format_area_truncated":
      return messages.issueFormatAreaTruncated;
    case "introduction_signature_mismatch":
      return messages.issueIntroductionSignatureMismatch;
    case "partition_area_misaligned":
      return messages.issuePartitionAreaMisaligned;
    case "chunk_header_truncated":
      return messages.issueChunkHeaderTruncated;
    case "non_empty_first_partition_is_sentinel":
      return messages.issueNonEmptyFirstPartitionIsSentinel;
    case "non_empty_partition_count_zero":
      return messages.issueNonEmptyPartitionCountZero;
    case "last_partition_data_length_out_of_range":
      return messages.issueLastPartitionDataLengthOutOfRange;
    case "first_partition_index_out_of_range":
      return messages.issueFirstPartitionIndexOutOfRange;
    case "partition_count_exceeds_allocated":
      return messages.issuePartitionCountExceedsAllocated;
    case "chunk_chain_duplicate_partition":
      return messages.issueChunkChainDuplicatePartition(Number(params.partitionIndex ?? -1));
    case "chunk_chain_cannot_read_next":
      return messages.issueChunkChainCannotReadNext(Number(params.partitionIndex ?? -1));
    case "chunk_chain_not_terminated":
      return messages.issueChunkChainNotTerminated;
    case "chunk_chain_ended_early":
      return messages.issueChunkChainEndedEarly;
    case "free_chain_head_invalid":
      return messages.issueFreeChainHeadInvalid;
    case "free_chain_duplicate_partition":
      return messages.issueFreeChainDuplicatePartition(Number(params.partitionIndex ?? -1));
    case "free_chain_cannot_read_next":
      return messages.issueFreeChainCannotReadNext(Number(params.partitionIndex ?? -1));
    case "free_chain_not_terminated":
      return messages.issueFreeChainNotTerminated;
    case "free_chain_ended_early":
      return messages.issueFreeChainEndedEarly;
    case "partition_overlap_between_chunks":
      return messages.issuePartitionOverlapBetweenChunks(
        Number(params.partitionIndex ?? -1),
        String(params.leftOwner ?? "-"),
        String(params.rightOwner ?? "-"),
      );
    case "free_partition_overlaps_owner":
      return messages.issueFreePartitionOverlapsOwner(
        Number(params.partitionIndex ?? -1),
        String(params.owner ?? "-"),
      );
    default:
      return messages.issueUnknown(issue.code);
  }
}

export function formatIssue(issue: RegionIssue, lang: string): string {
  const normalized = (lang ?? "zh") as LanguageCode;
  const messages = getMessages(normalized);
  return `${formatChunkScope(messages, issue)}${formatCoreIssue(messages, issue)}`;
}
