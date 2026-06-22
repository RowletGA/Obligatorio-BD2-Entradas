import { Navigate, Route, Routes } from "react-router-dom";
import { AppLayout } from "@/layouts/AppLayout";
import { AdminEventsPage } from "@/pages/AdminEventsPage";
import { AdminSectorsPage } from "@/pages/AdminSectorsPage";
import { AdminStadiumsPage } from "@/pages/AdminStadiumsPage";
import { DashboardPage } from "@/pages/DashboardPage";
import { EventsPage } from "@/pages/EventsPage";
import { LandingPage } from "@/pages/LandingPage";
import { PurchasePage } from "@/pages/PurchasePage";
import { PurchasesPage } from "@/pages/PurchasesPage";
import { RegistrationPage } from "@/pages/RegistrationPage";
import { ReportsPage } from "@/pages/ReportsPage";
import { TeamsPage } from "@/pages/TeamsPage";
import { TicketDetailPage } from "@/pages/TicketDetailPage";
import { TicketsPage } from "@/pages/TicketsPage";
import { TransfersPage } from "@/pages/TransfersPage";
import { ValidationPage } from "@/pages/ValidationPage";

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<LandingPage />} />
      <Route path="/app" element={<AppLayout />}>
        <Route index element={<DashboardPage />} />
        <Route path="registro" element={<RegistrationPage />} />
        <Route path="eventos" element={<EventsPage />} />
        <Route path="comprar" element={<PurchasePage />} />
        <Route path="entradas" element={<TicketsPage />} />
        <Route path="entradas/:id" element={<TicketDetailPage />} />
        <Route path="compras" element={<PurchasesPage />} />
        <Route path="transferencias" element={<TransfersPage />} />
        <Route path="admin/estadios" element={<AdminStadiumsPage />} />
        <Route path="admin/sectores" element={<AdminSectorsPage />} />
        <Route path="admin/eventos" element={<AdminEventsPage />} />
        <Route path="admin/equipos" element={<TeamsPage />} />
        <Route path="validacion" element={<ValidationPage />} />
        <Route path="reportes" element={<ReportsPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
