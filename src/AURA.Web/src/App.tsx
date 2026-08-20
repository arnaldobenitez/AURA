import { useCallback, useEffect, useState } from "react";
import { ProjectControlPage } from "./features/project-control/ProjectControlPage";
import { ProyectoDetalles } from "./features/project-details/ProyectoDetalles";
import { AppShell } from "./layout/AppShell";

const PROJECT_DETAILS_PREFIX = "/proyectos/";

function readLocation() {
  return window.location.pathname;
}

function App() {
  const [pathname, setPathname] = useState(readLocation);

  useEffect(() => {
    const handlePopState = () => setPathname(readLocation());
    window.addEventListener("popstate", handlePopState);
    return () => window.removeEventListener("popstate", handlePopState);
  }, []);

  const navigate = useCallback((nextPath: string) => {
    if (window.location.pathname !== nextPath) {
      window.history.pushState({}, "", nextPath);
    }
    setPathname(nextPath);
    window.scrollTo({ top: 0, behavior: "instant" });
  }, []);

  const projectKey = pathname.startsWith(PROJECT_DETAILS_PREFIX)
    ? decodeURIComponent(pathname.slice(PROJECT_DETAILS_PREFIX.length))
    : null;

  return (
    <AppShell onNavigate={navigate}>
      {projectKey ? (
        <ProyectoDetalles projectKey={projectKey} onBack={() => navigate("/")} />
      ) : (
        <ProjectControlPage
          onProjectSelect={(project) =>
            navigate(`${PROJECT_DETAILS_PREFIX}${encodeURIComponent(project.projectCode ?? String(project.projectId))}`)
          }
        />
      )}
    </AppShell>
  );
}

export default App;
