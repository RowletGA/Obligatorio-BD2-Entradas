import { createContext, useContext, useMemo, useState } from "react";
import { demoUsers } from "@/data/mockData";
import { apiClient, createInitialState } from "@/services/apiClient";
import type { AppData, DemoUser, DocumentoRef, Evento, PurchaseInput, Sector, Estadio, TransferInput } from "@/types";

interface AppContextValue {
  data: AppData;
  currentUser: DemoUser;
  setCurrentUser: (user: DemoUser) => void;
  demoUsers: DemoUser[];
  purchaseTickets: (input: PurchaseInput) => void;
  transferTicket: (input: TransferInput) => void;
  regenerateToken: (ticketId: string) => void;
  validateToken: (token: string, eventId: string) => ReturnType<typeof apiClient.validateToken>["result"];
  saveStadium: (stadium: Estadio) => void;
  deleteStadium: (stadiumId: string) => void;
  saveSector: (sector: Sector) => void;
  saveEvent: (event: Evento) => void;
  createRegistration: (user: DocumentoRef) => void;
}

const AppContext = createContext<AppContextValue | null>(null);

export const AppProvider = ({ children }: { children: React.ReactNode }) => {
  const [data, setData] = useState<AppData>(() => createInitialState());
  const [currentUser, setCurrentUser] = useState<DemoUser>(demoUsers[0]);

  const value = useMemo<AppContextValue>(
    () => ({
      data,
      currentUser,
      setCurrentUser,
      demoUsers,
      purchaseTickets: (input) => setData((current) => apiClient.buyTickets(current, currentUser.documento, input)),
      transferTicket: (input) => setData((current) => apiClient.transferTicket(current, currentUser.documento, input)),
      regenerateToken: (ticketId) => setData((current) => apiClient.regenerateToken(current, ticketId)),
      validateToken: (token, eventId) => {
        const device = data.dispositivos[0];
        const { data: nextData, result } = apiClient.validateToken(data, {
          token,
          eventId,
          funcionario: currentUser.documento,
          deviceId: device.idDispositivo,
        });
        setData(nextData);
        return result;
      },
      saveStadium: (stadium) => setData((current) => apiClient.saveStadium(current, stadium)),
      deleteStadium: (stadiumId) => setData((current) => apiClient.deleteStadium(current, stadiumId)),
      saveSector: (sector) => setData((current) => apiClient.saveSector(current, sector)),
      saveEvent: (event) => setData((current) => apiClient.saveEvent(current, event)),
      createRegistration: () => undefined,
    }),
    [currentUser, data],
  );

  return <AppContext.Provider value={value}>{children}</AppContext.Provider>;
};

export const useApp = () => {
  const context = useContext(AppContext);
  if (!context) throw new Error("useApp debe usarse dentro de AppProvider");
  return context;
};
