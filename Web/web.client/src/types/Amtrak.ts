export type AmtrakTrackedTrain = {
  id: number;
  trainNumber: string;
  isActive: boolean;
  createdAt: string;
  lastUpdate: string;
};

export type AmtrakPollingConfiguration = {
  id: number;
  pollIntervalMinutes: number;
  createdAt: string;
  lastUpdate: string;
};

export type CreateAmtrakTrackedTrain = {
  trainNumber: string;
};

export type UpdateAmtrakPollingConfiguration = {
  pollIntervalMinutes: number;
};