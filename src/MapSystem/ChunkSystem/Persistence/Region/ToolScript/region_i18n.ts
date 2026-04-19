export type LanguageCode = "zh" | "zh_tw" | "en" | "ja";

export interface Messages {
  appTitle: string;
  summary: string;
  layout: string;
  partitionDistribution: string;
  findings: string;
  freeChain: string;
  totals: string;
  aggregate: string;
  noFiles: string;
  filePath: string;
  createdAt: string;
  status: string;
  format: string;
  signature: string;
  alignment: string;
  ok: string;
  bad: string;
  loadedChunks: string;
  loadedChunkCapacityRatio: string;
  unusedChunks: string;
  unusedChunkRatio: string;
  declaredChunks: string;
  totalChunkCapacity: string;
  totalOccupiedBytes: string;
  actualDataBytes: string;
  totalInvalidBytes: string;
  prefixInvalidBytes: string;
  partitionAreaBytes: string;
  partitionInvalidBytes: string;
  writtenPartitionUnwrittenBytes: string;
  freePartitionBytes: string;
  activeNextPointerBytes: string;
  orphanPartitionBytes: string;
  allocatedPartitionCount: string;
  activePartitionCount: string;
  activePartitionCountHint: string;
  freePartitionCount: string;
  freePartitionCountHint: string;
  orphanPartitionCount: string;
  orphanPartitionCountHint: string;
  invalidRatioOfTotal: string;
  actualRatioOfTotal: string;
  partitionInvalidRatio: string;
  partitionValidRatio: string;
  preview: string;
  healthy: string;
  headLabel: string;
  bytesLabel: string;
  sentinelNoNext: string;
  usageInspector: string;
  usageInspectorHint: string;
  usageJson: string;
  usageJsonHint: string;
  usageFree: string;
  usageFreeHint: string;
  promptInputAnalyzePath: string;
  promptInputJsonPath: string;
  promptInputFreePath: string;
  invalidPathCancelled: string;
  exportCancelled: string;
  selectLanguageTitle: string;
  selectLanguageZh: string;
  selectLanguageZhTw: string;
  selectLanguageEn: string;
  selectLanguageJa: string;
  inspectorFailed: string;
  jsonFailed: string;
  freeChainFailed: string;
  distributionEmpty: string;
  chunkScope: (localX: number, localY: number) => string;
  partitionBucketLabel: (partitionCount: number) => string;
  issueUnknown: (code: string) => string;
  issueFormatAreaEmpty: string;
  issueFormatJsonParseFailed: (reason: string) => string;
  issueFormatTypeMismatch: (path: string) => string;
  issueFormatArrayLengthMismatch: (path: string) => string;
  issueFormatMissingField: (path: string) => string;
  issueFormatValueMismatch: (path: string) => string;
  issueFormatAreaTruncated: string;
  issueIntroductionSignatureMismatch: string;
  issuePartitionAreaMisaligned: string;
  issueChunkHeaderTruncated: string;
  issueNonEmptyFirstPartitionIsSentinel: string;
  issueNonEmptyPartitionCountZero: string;
  issueLastPartitionDataLengthOutOfRange: string;
  issueFirstPartitionIndexOutOfRange: string;
  issuePartitionCountExceedsAllocated: string;
  issueChunkChainDuplicatePartition: (partitionIndex: number) => string;
  issueChunkChainCannotReadNext: (partitionIndex: number) => string;
  issueChunkChainNotTerminated: string;
  issueChunkChainEndedEarly: string;
  issueFreeChainHeadInvalid: string;
  issueFreeChainDuplicatePartition: (partitionIndex: number) => string;
  issueFreeChainCannotReadNext: (partitionIndex: number) => string;
  issueFreeChainNotTerminated: string;
  issueFreeChainEndedEarly: string;
  issuePartitionOverlapBetweenChunks: (
    partitionIndex: number,
    leftOwner: string,
    rightOwner: string,
  ) => string;
  issueFreePartitionOverlapsOwner: (partitionIndex: number, owner: string) => string;
}

const common = {
  selectLanguageTitle: "language / 语言 / 語言 / 言語",
  selectLanguageZh: "[1] 简体中文",
  selectLanguageZhTw: "[2] 繁體中文",
  selectLanguageEn: "[3] English",
  selectLanguageJa: "[4] 日本語",
} as const;

const zh: Messages = {
  appTitle: "🧭 Region Inspector",
  summary: "📋 概览",
  layout: "📊 构成",
  partitionDistribution: "🧩 分区数分布",
  findings: "⚠️ 发现",
  freeChain: "🧵 空闲链",
  totals: "📦 汇总",
  aggregate: "全部文件汇总",
  noFiles: "没有找到 region 文件。",
  filePath: "文件",
  createdAt: "创建时间",
  status: "状态",
  format: "格式区",
  signature: "介绍签名",
  alignment: "分区对齐",
  ok: "正常",
  bad: "异常",
  loadedChunks: "加载区块数量",
  loadedChunkCapacityRatio: "加载区块占总区块数比",
  unusedChunks: "未加载区块数量",
  unusedChunkRatio: "未加载区块占总区块数比",
  declaredChunks: "声明非空区块数量",
  totalChunkCapacity: "总区块数",
  totalOccupiedBytes: "总占用大小",
  actualDataBytes: "实际数据大小",
  totalInvalidBytes: "总无效数据大小",
  prefixInvalidBytes: "前缀无效数据大小",
  partitionAreaBytes: "分区区总大小",
  partitionInvalidBytes: "分区内无效数据大小",
  writtenPartitionUnwrittenBytes: "分区数据内未写入区域大小",
  freePartitionBytes: "空闲分区大小",
  activeNextPointerBytes: "分区 next 指针大小",
  orphanPartitionBytes: "孤儿/泄漏分区大小",
  allocatedPartitionCount: "已分配分区数量",
  activePartitionCount: "使用分区数量",
  activePartitionCountHint: "即当前被已加载区块数据链实际占用的分区数量",
  freePartitionCount: "空闲分区数量",
  freePartitionCountHint: "即曾被使用、现已释放并等待再次使用的分区数量",
  orphanPartitionCount: "孤儿分区数量",
  orphanPartitionCountHint: "即既不在空闲链中、也未被任何区块链引用的分区数量",
  invalidRatioOfTotal: "无效数据占总占用比重",
  actualRatioOfTotal: "实际数据占总占用比重",
  partitionInvalidRatio: "分区无效数据比重",
  partitionValidRatio: "分区有效数据比重",
  preview: "链条预览",
  healthy: "未发现结构问题。",
  headLabel: "Head",
  bytesLabel: "大小",
  sentinelNoNext: "🧱无下一跳的哨兵值",
  usageInspector:
    "Usage: node --experimental-strip-types region_inspector.ts [path] [--lang zh|zh_tw|en|ja] [--no-recursive] [--max-chain-preview N] [--json]",
  usageInspectorHint: "如果未提供 path，将在启动后提示输入 region 文件或目录路径。",
  usageJson:
    "Usage: node --experimental-strip-types region_metrics_json.ts [path] [--lang zh|zh_tw|en|ja] [--no-recursive]",
  usageJsonHint: "如果未提供 path，将在启动后提示输入 region 文件或目录路径。",
  usageFree:
    "Usage: node --experimental-strip-types region_free_chain.ts [path] [--lang zh|zh_tw|en|ja] [--no-recursive] [--max-chain-preview N]",
  usageFreeHint: "如果未提供 path，将在启动后提示输入 region 文件或目录路径。",
  promptInputAnalyzePath: "请输入要分析的 region 文件或目录路径: ",
  promptInputJsonPath: "请输入要导出 JSON 的 region 文件或目录路径: ",
  promptInputFreePath: "请输入要查看空闲链的 region 文件或目录路径: ",
  invalidPathCancelled: "未提供有效路径，已取消分析。",
  exportCancelled: "未提供有效路径，已取消导出。",
  inspectorFailed: "Region inspector 执行失败",
  jsonFailed: "Region metrics JSON 导出失败",
  freeChainFailed: "Region 空闲链分析失败",
  distributionEmpty: "没有健康加载区块。",
  chunkScope: (localX, localY) => `chunk(${localX},${localY})`,
  partitionBucketLabel: (partitionCount) => `${partitionCount} 分区区块`,
  issueUnknown: (code) => `未知问题: ${code}`,
  issueFormatAreaEmpty: "format 区在去掉尾部 0 后为空",
  issueFormatJsonParseFailed: (reason) => `format JSON 解析失败: ${reason}`,
  issueFormatTypeMismatch: (path) => `${path} 类型不匹配`,
  issueFormatArrayLengthMismatch: (path) => `${path} 数组长度不匹配`,
  issueFormatMissingField: (path) => `${path} 缺失`,
  issueFormatValueMismatch: (path) => `${path} 值不匹配`,
  issueFormatAreaTruncated: "format 区长度不足",
  issueIntroductionSignatureMismatch: "introduction 签名不匹配",
  issuePartitionAreaMisaligned: "分区区长度没有按 4096 对齐",
  issueChunkHeaderTruncated: "chunk header 区被截断",
  issueNonEmptyFirstPartitionIsSentinel: "非空头记录的 firstPartitionIndex 仍为哨兵值",
  issueNonEmptyPartitionCountZero: "非空头记录的 partitionCount 为 0",
  issueLastPartitionDataLengthOutOfRange: "lastPartitionDataLength 超出有效范围",
  issueFirstPartitionIndexOutOfRange: "firstPartitionIndex 越界",
  issuePartitionCountExceedsAllocated: "partitionCount 超过已分配分区总数",
  issueChunkChainDuplicatePartition: (partitionIndex) => `chunk 链重复引用分区 ${partitionIndex}`,
  issueChunkChainCannotReadNext: (partitionIndex) => `无法读取分区 ${partitionIndex} 的 next`,
  issueChunkChainNotTerminated: "chunk 链最后一个分区没有以哨兵结束",
  issueChunkChainEndedEarly: "chunk 链在达到声明分区数之前提前结束",
  issueFreeChainHeadInvalid: "空闲链头状态非法",
  issueFreeChainDuplicatePartition: (partitionIndex) => `空闲链重复引用分区 ${partitionIndex}`,
  issueFreeChainCannotReadNext: (partitionIndex) => `无法读取空闲分区 ${partitionIndex} 的 next`,
  issueFreeChainNotTerminated: "空闲链最后一个分区没有以哨兵结束",
  issueFreeChainEndedEarly: "空闲链在达到 freePartitionCount 之前提前结束",
  issuePartitionOverlapBetweenChunks: (partitionIndex, leftOwner, rightOwner) =>
    `分区 ${partitionIndex} 同时被 ${leftOwner} 和 ${rightOwner} 引用`,
  issueFreePartitionOverlapsOwner: (partitionIndex, owner) => `空闲分区 ${partitionIndex} 与 ${owner} 重叠`,
  ...common,
};

const zhTw: Messages = {
  appTitle: "🧭 Region Inspector",
  summary: "📋 概覽",
  layout: "📊 構成",
  partitionDistribution: "🧩 分區數分布",
  findings: "⚠️ 發現",
  freeChain: "🧵 空閒鏈",
  totals: "📦 彙總",
  aggregate: "全部檔案彙總",
  noFiles: "沒有找到 region 檔案。",
  filePath: "檔案",
  createdAt: "建立時間",
  status: "狀態",
  format: "格式區",
  signature: "介紹簽名",
  alignment: "分區對齊",
  ok: "正常",
  bad: "異常",
  loadedChunks: "載入區塊數量",
  loadedChunkCapacityRatio: "載入區塊占總區塊數比",
  unusedChunks: "未載入區塊數量",
  unusedChunkRatio: "未載入區塊占總區塊數比",
  declaredChunks: "宣告非空區塊數量",
  totalChunkCapacity: "總區塊數",
  totalOccupiedBytes: "總占用大小",
  actualDataBytes: "實際資料大小",
  totalInvalidBytes: "總無效資料大小",
  prefixInvalidBytes: "前綴無效資料大小",
  partitionAreaBytes: "分區區總大小",
  partitionInvalidBytes: "分區內無效資料大小",
  writtenPartitionUnwrittenBytes: "分區資料內未寫入區域大小",
  freePartitionBytes: "空閒分區大小",
  activeNextPointerBytes: "分區 next 指標大小",
  orphanPartitionBytes: "孤兒/洩漏分區大小",
  allocatedPartitionCount: "已分配分區數量",
  activePartitionCount: "使用分區數量",
  activePartitionCountHint: "即目前被已載入區塊資料鏈實際占用的分區數量",
  freePartitionCount: "空閒分區數量",
  freePartitionCountHint: "即曾被使用、現已釋放並等待再次使用的分區數量",
  orphanPartitionCount: "孤兒分區數量",
  orphanPartitionCountHint: "即既不在空閒鏈中、也未被任何區塊鏈引用的分區數量",
  invalidRatioOfTotal: "無效資料占總占用比重",
  actualRatioOfTotal: "實際資料占總占用比重",
  partitionInvalidRatio: "分區無效資料比重",
  partitionValidRatio: "分區有效資料比重",
  preview: "鏈條預覽",
  healthy: "未發現結構問題。",
  headLabel: "Head",
  bytesLabel: "大小",
  sentinelNoNext: "🧱無下一跳的哨兵值",
  usageInspector:
    "Usage: node --experimental-strip-types region_inspector.ts [path] [--lang zh|zh_tw|en|ja] [--no-recursive] [--max-chain-preview N] [--json]",
  usageInspectorHint: "如果未提供 path，將在啟動後提示輸入 region 檔案或目錄路徑。",
  usageJson:
    "Usage: node --experimental-strip-types region_metrics_json.ts [path] [--lang zh|zh_tw|en|ja] [--no-recursive]",
  usageJsonHint: "如果未提供 path，將在啟動後提示輸入 region 檔案或目錄路徑。",
  usageFree:
    "Usage: node --experimental-strip-types region_free_chain.ts [path] [--lang zh|zh_tw|en|ja] [--no-recursive] [--max-chain-preview N]",
  usageFreeHint: "如果未提供 path，將在啟動後提示輸入 region 檔案或目錄路徑。",
  promptInputAnalyzePath: "請輸入要分析的 region 檔案或目錄路徑: ",
  promptInputJsonPath: "請輸入要匯出 JSON 的 region 檔案或目錄路徑: ",
  promptInputFreePath: "請輸入要查看空閒鏈的 region 檔案或目錄路徑: ",
  invalidPathCancelled: "未提供有效路徑，已取消分析。",
  exportCancelled: "未提供有效路徑，已取消匯出。",
  inspectorFailed: "Region inspector 執行失敗",
  jsonFailed: "Region metrics JSON 匯出失敗",
  freeChainFailed: "Region 空閒鏈分析失敗",
  distributionEmpty: "沒有健康載入區塊。",
  chunkScope: (localX, localY) => `chunk(${localX},${localY})`,
  partitionBucketLabel: (partitionCount) => `${partitionCount} 分區區塊`,
  issueUnknown: (code) => `未知問題: ${code}`,
  issueFormatAreaEmpty: "format 區在去掉尾部 0 後為空",
  issueFormatJsonParseFailed: (reason) => `format JSON 解析失敗: ${reason}`,
  issueFormatTypeMismatch: (path) => `${path} 類型不匹配`,
  issueFormatArrayLengthMismatch: (path) => `${path} 陣列長度不匹配`,
  issueFormatMissingField: (path) => `${path} 缺失`,
  issueFormatValueMismatch: (path) => `${path} 值不匹配`,
  issueFormatAreaTruncated: "format 區長度不足",
  issueIntroductionSignatureMismatch: "introduction 簽名不匹配",
  issuePartitionAreaMisaligned: "分區區長度沒有按 4096 對齊",
  issueChunkHeaderTruncated: "chunk header 區被截斷",
  issueNonEmptyFirstPartitionIsSentinel: "非空頭記錄的 firstPartitionIndex 仍為哨兵值",
  issueNonEmptyPartitionCountZero: "非空頭記錄的 partitionCount 為 0",
  issueLastPartitionDataLengthOutOfRange: "lastPartitionDataLength 超出有效範圍",
  issueFirstPartitionIndexOutOfRange: "firstPartitionIndex 越界",
  issuePartitionCountExceedsAllocated: "partitionCount 超過已分配分區總數",
  issueChunkChainDuplicatePartition: (partitionIndex) => `chunk 鏈重複引用分區 ${partitionIndex}`,
  issueChunkChainCannotReadNext: (partitionIndex) => `無法讀取分區 ${partitionIndex} 的 next`,
  issueChunkChainNotTerminated: "chunk 鏈最後一個分區沒有以哨兵結束",
  issueChunkChainEndedEarly: "chunk 鏈在達到宣告分區數之前提前結束",
  issueFreeChainHeadInvalid: "空閒鏈頭狀態非法",
  issueFreeChainDuplicatePartition: (partitionIndex) => `空閒鏈重複引用分區 ${partitionIndex}`,
  issueFreeChainCannotReadNext: (partitionIndex) => `無法讀取空閒分區 ${partitionIndex} 的 next`,
  issueFreeChainNotTerminated: "空閒鏈最後一個分區沒有以哨兵結束",
  issueFreeChainEndedEarly: "空閒鏈在達到 freePartitionCount 之前提前結束",
  issuePartitionOverlapBetweenChunks: (partitionIndex, leftOwner, rightOwner) =>
    `分區 ${partitionIndex} 同時被 ${leftOwner} 和 ${rightOwner} 引用`,
  issueFreePartitionOverlapsOwner: (partitionIndex, owner) => `空閒分區 ${partitionIndex} 與 ${owner} 重疊`,
  ...common,
};

const en: Messages = {
  appTitle: "🧭 Region Inspector",
  summary: "📋 Summary",
  layout: "📊 Composition",
  partitionDistribution: "🧩 Partition Count Distribution",
  findings: "⚠️ Findings",
  freeChain: "🧵 Free Chain",
  totals: "📦 Totals",
  aggregate: "Aggregate summary",
  noFiles: "No region files were found.",
  filePath: "File",
  createdAt: "Created",
  status: "Status",
  format: "Format",
  signature: "Signature",
  alignment: "Alignment",
  ok: "OK",
  bad: "Bad",
  loadedChunks: "Loaded chunks",
  loadedChunkCapacityRatio: "Loaded chunks / total chunk slots",
  unusedChunks: "Unloaded chunks",
  unusedChunkRatio: "Unloaded chunks / total chunk slots",
  declaredChunks: "Declared non-empty chunks",
  totalChunkCapacity: "Total chunk slots",
  totalOccupiedBytes: "Total occupied bytes",
  actualDataBytes: "Actual data bytes",
  totalInvalidBytes: "Total invalid bytes",
  prefixInvalidBytes: "Prefix invalid bytes",
  partitionAreaBytes: "Partition area bytes",
  partitionInvalidBytes: "Partition invalid bytes",
  writtenPartitionUnwrittenBytes: "Unwritten bytes inside partition data",
  freePartitionBytes: "Free partition bytes",
  activeNextPointerBytes: "Partition next-pointer bytes",
  orphanPartitionBytes: "Orphan / leaked partition bytes",
  allocatedPartitionCount: "Allocated partitions",
  activePartitionCount: "Used partitions",
  activePartitionCountHint: "Partitions currently occupied by loaded chunk data chains",
  freePartitionCount: "Free partitions",
  freePartitionCountHint: "Partitions that were used before, then released and kept for reuse",
  orphanPartitionCount: "Orphan partitions",
  orphanPartitionCountHint: "Partitions that are neither in the free chain nor referenced by any chunk chain",
  invalidRatioOfTotal: "Invalid / total",
  actualRatioOfTotal: "Actual / total",
  partitionInvalidRatio: "Partition invalid ratio",
  partitionValidRatio: "Partition valid ratio",
  preview: "Chain preview",
  healthy: "No structural findings.",
  headLabel: "Head",
  bytesLabel: "Bytes",
  sentinelNoNext: "🧱 no-next sentinel",
  usageInspector:
    "Usage: node --experimental-strip-types region_inspector.ts [path] [--lang zh|zh_tw|en|ja] [--no-recursive] [--max-chain-preview N] [--json]",
  usageInspectorHint: "If path is omitted, the script will prompt for a region file or directory path.",
  usageJson:
    "Usage: node --experimental-strip-types region_metrics_json.ts [path] [--lang zh|zh_tw|en|ja] [--no-recursive]",
  usageJsonHint: "If path is omitted, the script will prompt for a region file or directory path.",
  usageFree:
    "Usage: node --experimental-strip-types region_free_chain.ts [path] [--lang zh|zh_tw|en|ja] [--no-recursive] [--max-chain-preview N]",
  usageFreeHint: "If path is omitted, the script will prompt for a region file or directory path.",
  promptInputAnalyzePath: "Enter the region file or directory path to inspect: ",
  promptInputJsonPath: "Enter the region file or directory path to export as JSON: ",
  promptInputFreePath: "Enter the region file or directory path to inspect the free chain: ",
  invalidPathCancelled: "No valid path was provided. Inspection cancelled.",
  exportCancelled: "No valid path was provided. Export cancelled.",
  inspectorFailed: "Region inspector failed",
  jsonFailed: "Region metrics JSON export failed",
  freeChainFailed: "Region free-chain inspection failed",
  distributionEmpty: "No healthy loaded chunks.",
  chunkScope: (localX, localY) => `chunk(${localX},${localY})`,
  partitionBucketLabel: (partitionCount) =>
    `${partitionCount}-partition ${partitionCount === 1 ? "chunk" : "chunks"}`,
  issueUnknown: (code) => `Unknown issue: ${code}`,
  issueFormatAreaEmpty: "Format area is empty after trimming trailing zero bytes",
  issueFormatJsonParseFailed: (reason) => `Failed to parse format JSON: ${reason}`,
  issueFormatTypeMismatch: (path) => `Type mismatch at ${path}`,
  issueFormatArrayLengthMismatch: (path) => `Array length mismatch at ${path}`,
  issueFormatMissingField: (path) => `Missing field at ${path}`,
  issueFormatValueMismatch: (path) => `Value mismatch at ${path}`,
  issueFormatAreaTruncated: "Format area is truncated",
  issueIntroductionSignatureMismatch: "Introduction signature mismatch",
  issuePartitionAreaMisaligned: "Partition area size is not aligned to 4096 bytes",
  issueChunkHeaderTruncated: "Chunk header area is truncated",
  issueNonEmptyFirstPartitionIsSentinel: "Non-empty chunk header still uses the sentinel firstPartitionIndex",
  issueNonEmptyPartitionCountZero: "Non-empty chunk header has partitionCount = 0",
  issueLastPartitionDataLengthOutOfRange: "lastPartitionDataLength is out of valid range",
  issueFirstPartitionIndexOutOfRange: "firstPartitionIndex is out of range",
  issuePartitionCountExceedsAllocated: "partitionCount exceeds allocated partition count",
  issueChunkChainDuplicatePartition: (partitionIndex) => `Chunk chain reuses partition ${partitionIndex}`,
  issueChunkChainCannotReadNext: (partitionIndex) => `Cannot read next for partition ${partitionIndex}`,
  issueChunkChainNotTerminated: "Chunk chain does not terminate with the sentinel on its last partition",
  issueChunkChainEndedEarly: "Chunk chain ends before reaching the declared partition count",
  issueFreeChainHeadInvalid: "Free-chain head state is invalid",
  issueFreeChainDuplicatePartition: (partitionIndex) => `Free chain reuses partition ${partitionIndex}`,
  issueFreeChainCannotReadNext: (partitionIndex) => `Cannot read next for free partition ${partitionIndex}`,
  issueFreeChainNotTerminated: "Free chain does not terminate with the sentinel on its last partition",
  issueFreeChainEndedEarly: "Free chain ends before reaching freePartitionCount",
  issuePartitionOverlapBetweenChunks: (partitionIndex, leftOwner, rightOwner) =>
    `Partition ${partitionIndex} is referenced by both ${leftOwner} and ${rightOwner}`,
  issueFreePartitionOverlapsOwner: (partitionIndex, owner) =>
    `Free partition ${partitionIndex} overlaps with ${owner}`,
  ...common,
};

const ja: Messages = {
  appTitle: "🧭 Region Inspector",
  summary: "📋 概要",
  layout: "📊 構成",
  partitionDistribution: "🧩 パーティション数分布",
  findings: "⚠️ 検出事項",
  freeChain: "🧵 空きチェーン",
  totals: "📦 集計",
  aggregate: "全ファイル集計",
  noFiles: "region ファイルが見つかりませんでした。",
  filePath: "ファイル",
  createdAt: "作成時刻",
  status: "状態",
  format: "フォーマット",
  signature: "署名",
  alignment: "整列",
  ok: "正常",
  bad: "異常",
  loadedChunks: "読み込み済みチャンク数",
  loadedChunkCapacityRatio: "読み込みチャンク / 総チャンク数",
  unusedChunks: "未読み込みチャンク数",
  unusedChunkRatio: "未読み込みチャンク / 総チャンク数",
  declaredChunks: "宣言済み非空チャンク数",
  totalChunkCapacity: "総チャンク数",
  totalOccupiedBytes: "総占有サイズ",
  actualDataBytes: "実データサイズ",
  totalInvalidBytes: "総無効データサイズ",
  prefixInvalidBytes: "先頭固定無効サイズ",
  partitionAreaBytes: "パーティション領域総サイズ",
  partitionInvalidBytes: "パーティション内無効データサイズ",
  writtenPartitionUnwrittenBytes: "パーティションデータ内未書き込み領域サイズ",
  freePartitionBytes: "空きパーティションサイズ",
  activeNextPointerBytes: "パーティション next ポインタサイズ",
  orphanPartitionBytes: "孤立/リークパーティションサイズ",
  allocatedPartitionCount: "割り当て済みパーティション数",
  activePartitionCount: "使用中パーティション数",
  activePartitionCountHint: "現在、読み込み済みチャンクのデータチェーンが実際に占有している数",
  freePartitionCount: "空きパーティション数",
  freePartitionCountHint: "以前使われ、現在は解放されて再利用待ちになっている数",
  orphanPartitionCount: "孤立パーティション数",
  orphanPartitionCountHint: "空きチェーンにもなく、どのチャンクチェーンからも参照されていない数",
  invalidRatioOfTotal: "無効 / 総占有",
  actualRatioOfTotal: "実データ / 総占有",
  partitionInvalidRatio: "パーティション無効比率",
  partitionValidRatio: "パーティション有効比率",
  preview: "チェーン表示",
  healthy: "構造上の問題は見つかりませんでした。",
  headLabel: "Head",
  bytesLabel: "サイズ",
  sentinelNoNext: "🧱 次がない番兵値",
  usageInspector:
    "Usage: node --experimental-strip-types region_inspector.ts [path] [--lang zh|zh_tw|en|ja] [--no-recursive] [--max-chain-preview N] [--json]",
  usageInspectorHint: "path を省略した場合は、起動後に region ファイルまたはディレクトリのパス入力を求めます。",
  usageJson:
    "Usage: node --experimental-strip-types region_metrics_json.ts [path] [--lang zh|zh_tw|en|ja] [--no-recursive]",
  usageJsonHint: "path を省略した場合は、起動後に region ファイルまたはディレクトリのパス入力を求めます。",
  usageFree:
    "Usage: node --experimental-strip-types region_free_chain.ts [path] [--lang zh|zh_tw|en|ja] [--no-recursive] [--max-chain-preview N]",
  usageFreeHint: "path を省略した場合は、起動後に region ファイルまたはディレクトリのパス入力を求めます。",
  promptInputAnalyzePath: "解析する region ファイルまたはディレクトリのパスを入力してください: ",
  promptInputJsonPath: "JSON を出力する region ファイルまたはディレクトリのパスを入力してください: ",
  promptInputFreePath: "空きチェーンを確認する region ファイルまたはディレクトリのパスを入力してください: ",
  invalidPathCancelled: "有効なパスが入力されなかったため、解析を中止しました。",
  exportCancelled: "有効なパスが入力されなかったため、出力を中止しました。",
  inspectorFailed: "Region inspector の実行に失敗しました",
  jsonFailed: "Region metrics JSON の出力に失敗しました",
  freeChainFailed: "空きチェーン解析に失敗しました",
  distributionEmpty: "健全な読み込みチャンクがありません。",
  chunkScope: (localX, localY) => `chunk(${localX},${localY})`,
  partitionBucketLabel: (partitionCount) => `${partitionCount} パーティションチャンク`,
  issueUnknown: (code) => `未知の問題: ${code}`,
  issueFormatAreaEmpty: "末尾の 0 を除いた後の format 領域が空です",
  issueFormatJsonParseFailed: (reason) => `format JSON の解析に失敗しました: ${reason}`,
  issueFormatTypeMismatch: (path) => `${path} の型が一致しません`,
  issueFormatArrayLengthMismatch: (path) => `${path} の配列長が一致しません`,
  issueFormatMissingField: (path) => `${path} が欠落しています`,
  issueFormatValueMismatch: (path) => `${path} の値が一致しません`,
  issueFormatAreaTruncated: "format 領域の長さが不足しています",
  issueIntroductionSignatureMismatch: "introduction 署名が一致しません",
  issuePartitionAreaMisaligned: "パーティション領域サイズが 4096 バイト境界に揃っていません",
  issueChunkHeaderTruncated: "chunk header 領域が途中で切れています",
  issueNonEmptyFirstPartitionIsSentinel: "非空ヘッダの firstPartitionIndex がまだ番兵値です",
  issueNonEmptyPartitionCountZero: "非空ヘッダの partitionCount が 0 です",
  issueLastPartitionDataLengthOutOfRange: "lastPartitionDataLength が有効範囲外です",
  issueFirstPartitionIndexOutOfRange: "firstPartitionIndex が範囲外です",
  issuePartitionCountExceedsAllocated: "partitionCount が割り当て済みパーティション数を超えています",
  issueChunkChainDuplicatePartition: (partitionIndex) => `chunk チェーンがパーティション ${partitionIndex} を重複参照しています`,
  issueChunkChainCannotReadNext: (partitionIndex) => `パーティション ${partitionIndex} の next を読み取れません`,
  issueChunkChainNotTerminated: "chunk チェーンの最後のパーティションが番兵で終端していません",
  issueChunkChainEndedEarly: "chunk チェーンが宣言パーティション数に達する前に終了しています",
  issueFreeChainHeadInvalid: "空きチェーンの先頭状態が不正です",
  issueFreeChainDuplicatePartition: (partitionIndex) => `空きチェーンがパーティション ${partitionIndex} を重複参照しています`,
  issueFreeChainCannotReadNext: (partitionIndex) => `空きパーティション ${partitionIndex} の next を読み取れません`,
  issueFreeChainNotTerminated: "空きチェーンの最後のパーティションが番兵で終端していません",
  issueFreeChainEndedEarly: "空きチェーンが freePartitionCount に達する前に終了しています",
  issuePartitionOverlapBetweenChunks: (partitionIndex, leftOwner, rightOwner) =>
    `パーティション ${partitionIndex} が ${leftOwner} と ${rightOwner} の両方から参照されています`,
  issueFreePartitionOverlapsOwner: (partitionIndex, owner) =>
    `空きパーティション ${partitionIndex} が ${owner} と重複しています`,
  ...common,
};

const TABLE: Record<LanguageCode, Messages> = {
  zh,
  zh_tw: zhTw,
  en,
  ja,
};

export function getMessages(lang: string): Messages {
  return TABLE[normalizeLanguageCode(lang) ?? "zh"];
}

export function normalizeLanguageCode(lang: string | undefined): LanguageCode | null {
  if (!lang) {
    return null;
  }

  const normalized = lang.trim().toLowerCase();
  if (normalized === "zh" || normalized === "zh_tw" || normalized === "en" || normalized === "ja") {
    return normalized;
  }
  return null;
}
