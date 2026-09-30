import { createBrowserRouter } from 'react-router-dom';
import App from './App.jsx';
import Login from './components/Login.jsx';
import Register from './components/Register.jsx';
import Activate from './components/Activate.jsx';
import EventList from './components/EventList.jsx';
import CreateEvent from './components/CreateEvent.jsx';
import EventDetail from './components/EventDetail.jsx';
import RequireRoleRoute from './components/RequireRoleRoute.jsx';

export const router = createBrowserRouter([
  {
    path: '/',
    element: <App />,
    children: [
      {
        path: 'login',
        element: <Login />,
      },
      {
        path: 'register',
        element: <Register />,
      },
      {
        path: 'activate',
        element: <Activate />,
      },
      {
        path: 'events',
        element: (
          <RequireRoleRoute allowedRoles={['Admin', 'Organizer']}>
            <EventList />
          </RequireRoleRoute>
        ),
      },
      {
        path: 'events/create',
        element: (
          <RequireRoleRoute allowedRoles={['Admin', 'Organizer']}>
            <CreateEvent />
          </RequireRoleRoute>
        ),
      },
      {
        path: 'events/:id',
        element: (
          <RequireRoleRoute allowedRoles={['Admin', 'Organizer']}>
            <EventDetail />
          </RequireRoleRoute>
        ),
      },
    ],
  },
]);
