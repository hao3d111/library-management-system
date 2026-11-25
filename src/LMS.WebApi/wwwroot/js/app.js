import { clearSession, getSession, apiRequest } from './api.js';
import { renderBooksView } from './views/booksView.js';
import { renderCategoriesView } from './views/categoriesView.js';
import { renderMembersView } from './views/membersView.js';
import { renderUsersView } from './views/usersView.js';
import { renderBorrowingsView } from './views/borrowingsView.js';
import { renderHistoryView } from './views/historyView.js';
import { renderOverdueView } from './views/overdueView.js';
import { renderDashboardView } from './views/dashboardView.js';
import { renderSettingsView } from './views/settingsView.js';

const session = getSession();
if (!session?.token || !session?.user) {
  window.location.href = '/index.html';
}

const ROUTES = {
  dashboard: {
    title: 'Dashboard & Statistical Reports',
    roles: ['Administrator'],
    render: renderDashboardView
  },
  books: {
    title: 'Book Management',
    roles: ['Administrator', 'Librarian'],
    render: (container, actions, user) => renderBooksView(container, actions, user, { canManage: true })
  },
  catalog: {
    title: 'Book Catalog & Search',
    roles: ['LibraryMember', 'Librarian', 'Administrator'],
    render: (container, actions, user) => renderBooksView(container, actions, user, { canManage: false })
  },
  categories: {
    title: 'Book Categories',
    roles: ['Administrator', 'Librarian'],
    render: renderCategoriesView
  },
  members: {
    title: 'Library Members',
    roles: ['Administrator', 'Librarian'],
    render: renderMembersView
  },
  borrowings: {
    title: 'Borrow & Return Desk',
    roles: ['Administrator', 'Librarian'],
    render: renderBorrowingsView
  },
  overdue: {
    title: 'Overdue Monitoring & Fines',
    roles: ['Administrator', 'Librarian'],
    render: renderOverdueView
  },
  history: {
    title: 'My Borrowing History',
    roles: ['LibraryMember'],
    render: renderHistoryView
  },
  users: {
    title: 'User Accounts & Roles',
    roles: ['Administrator'],
    render: renderUsersView
  },
  settings: {
    title: 'System Settings',
    roles: ['Administrator'],
    render: renderSettingsView
  }
};

const MENU_BY_ROLE = {
  Administrator: [
    { hash: 'dashboard', label: 'Dashboard & Reports' },
    { hash: 'users', label: 'Users & Roles' },
    { hash: 'settings', label: 'System Settings' },
    { hash: 'books', label: 'Books' },
    { hash: 'categories', label: 'Categories' },
    { hash: 'members', label: 'Members' },
    { hash: 'borrowings', label: 'Borrow & Return' },
    { hash: 'overdue', label: 'Overdue Monitor' }
  ],
  Librarian: [
    { hash: 'books', label: 'Books' },
    { hash: 'categories', label: 'Categories' },
    { hash: 'members', label: 'Members' },
    { hash: 'borrowings', label: 'Borrow & Return' },
    { hash: 'overdue', label: 'Overdue Monitor' }
  ],
  LibraryMember: [
    { hash: 'catalog', label: 'Book Catalog' },
    { hash: 'history', label: 'My Borrowing History' }
  ]
};

function defaultRouteForRole(role) {
  if (role === 'Administrator') return 'dashboard';
  if (role === 'Librarian') return 'books';
  return 'catalog';
}

async function init() {
  try {
    const profile = await apiRequest('/api/auth/me');
    session.user = profile;
  } catch {
    clearSession();
    window.location.href = '/index.html';
    return;
  }

  const user = session.user;
  document.getElementById('sidebar-role').textContent = user.role;
  document.getElementById('user-name').textContent = user.fullName;
  document.getElementById('user-meta').textContent = user.membershipCode
    ? `${user.username} • ${user.membershipCode}`
    : user.username;

  document.getElementById('logout-btn').addEventListener('click', () => {
    clearSession();
    window.location.href = '/index.html';
  });

  renderNav(user.role);
  window.addEventListener('hashchange', () => renderCurrentRoute(user));
  renderCurrentRoute(user);
}

function renderNav(role) {
  const nav = document.getElementById('nav-list');
  const items = MENU_BY_ROLE[role] || [];
  nav.innerHTML = items
    .map((item) => `<li><a class="nav-link" data-route="${item.hash}" href="#${item.hash}">${item.label}</a></li>`)
    .join('');
}

async function renderCurrentRoute(user) {
  let routeKey = (window.location.hash || '').replace('#', '').trim();
  let route = ROUTES[routeKey];

  if (!route || !route.roles.includes(user.role)) {
    routeKey = defaultRouteForRole(user.role);
    history.replaceState(null, '', `#${routeKey}`);
    route = ROUTES[routeKey];
  }

  document.querySelectorAll('.nav-link').forEach((link) => {
    link.classList.toggle('active', link.dataset.route === routeKey);
  });

  document.getElementById('page-title').textContent = route.title;
  const actions = document.getElementById('topbar-actions');
  const container = document.getElementById('page-content');
  actions.innerHTML = '';
  container.innerHTML = '<div class="card">Loading...</div>';

  await route.render(container, actions, user);
}

init();
