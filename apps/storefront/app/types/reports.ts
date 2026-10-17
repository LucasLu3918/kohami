export type ReportRow = {
  date: string
  sourceKind: 'Primary' | 'Late' | 'Extra'
  orderCount: number
  collectedTwd: number
  succeededRefundTwd: number
  pendingTwd: number
  availableTwd: number
  netConfirmedTwd: number
  discrepancyCount: number
}

export type AggregateReport = {
  mode: 'mock'
  cutoff: string
  rows: ReportRow[]
  notice: string
}
