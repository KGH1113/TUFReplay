export interface TextDownloadTicket {
  url: string;
  byteLength: number;
}

/** File bytes travel separately from bounded local control messages. */
export interface TextDownloads {
  readText(ticket: TextDownloadTicket, options?: { signal?: AbortSignal }): Promise<string>;
}
