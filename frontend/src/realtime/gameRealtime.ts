import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'

export function createGameConnection() {
  return new HubConnectionBuilder()
    .withUrl('/api/game')
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()
}

export function isConnectionReady(connection: HubConnection | null) {
  return connection?.state === HubConnectionState.Connected
}